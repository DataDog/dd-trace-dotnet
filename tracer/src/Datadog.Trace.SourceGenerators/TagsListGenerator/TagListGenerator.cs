// <copyright file="TagListGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.SourceGenerators.TagsListGenerator.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Datadog.Trace.SourceGenerators.TagsListGenerator
{
    /// <inheritdoc />
    [Generator]
    public class TagListGenerator : IIncrementalGenerator
    {
        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Register the attribute source
            context.RegisterPostInitializationOutput(ctx => ctx.AddSource("TagNameAttribute.g.cs", Sources.TagNameAttribute));

            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations =
                context.SyntaxProvider.CreateSyntaxProvider(IsAttributedProperty, GetPotentialClassesForGeneration)
                       .Where(static m => m is not null)!;

            IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses =
                context.CompilationProvider.Combine(classDeclarations.Collect());

            context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Item1, source.Item2, spc));
        }

        private static bool IsAttributedProperty(SyntaxNode node, CancellationToken cancellationToken)
            => node is PropertyDeclarationSyntax m && m.AttributeLists.Count > 0;

        private static ClassDeclarationSyntax? GetPotentialClassesForGeneration(GeneratorSyntaxContext context, CancellationToken cancellationToken)
        {
            var propertyDeclarationSyntax = (PropertyDeclarationSyntax)context.Node;

            foreach (AttributeListSyntax attributeListSyntax in propertyDeclarationSyntax.AttributeLists)
            {
                foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                {
                    IMethodSymbol? attributeSymbol = context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol as IMethodSymbol;
                    if (attributeSymbol is null)
                    {
                        continue;
                    }

                    INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                    string fullName = attributeContainingTypeSymbol.ToDisplayString();

                    if (fullName == Constants.TagNameAttribute)
                    {
                        return propertyDeclarationSyntax.Parent as ClassDeclarationSyntax;
                    }
                }
            }

            return null;
        }

        private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
        {
            if (classes.IsDefaultOrEmpty)
            {
                // nothing to do yet
                return;
            }

            IEnumerable<ClassDeclarationSyntax> distinctClasses = classes.Distinct();

            var tagLists = GetTagLists(compilation, distinctClasses, context.ReportDiagnostic, context.CancellationToken);
            if (tagLists.Count > 0)
            {
                    var source = Sources.CreateTagsList(tagLists);
                    context.AddSource("TagsList.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }

        private static IReadOnlyList<TagList> GetTagLists(
            Compilation compilation,
            IEnumerable<ClassDeclarationSyntax> classes,
            Action<Diagnostic> reportDiagnostic,
            CancellationToken cancellationToken)
        {
            INamedTypeSymbol? tagNameAttribute = compilation.GetTypeByMetadataName(Constants.TagNameAttribute);
            if (tagNameAttribute == null)
            {
                // nothing to do if this type isn't available
                return Array.Empty<TagList>();
            }

            var results = new List<TagList>();

            // we enumerate by syntax tree, to minimize the need to instantiate semantic models (since they're expensive)
            foreach (var group in classes.GroupBy(x => x.SyntaxTree))
            {
                SemanticModel? sm = null;
                foreach (ClassDeclarationSyntax classDec in group)
                {
                    // stop if we're asked to
                    cancellationToken.ThrowIfCancellationRequested();

                    List<PropertyTag>? properties = null;

                    foreach (MemberDeclarationSyntax member in classDec.Members)
                    {
                        var property = member as PropertyDeclarationSyntax;
                        if (property is null)
                        {
                            // we only care about properties
                            continue;
                        }

                        sm ??= compilation.GetSemanticModel(classDec.SyntaxTree);
                        IPropertySymbol? tagPropertySymbol = sm.GetDeclaredSymbol(property, cancellationToken) as IPropertySymbol;
                        Debug.Assert(tagPropertySymbol is not null, "Tagged property is not present");
                        string? tagValue = null;

                        foreach (AttributeListSyntax attributeList in property.AttributeLists)
                        {
                            foreach (AttributeSyntax attributeSyntax in attributeList.Attributes)
                            {
                                IMethodSymbol? attrCtorSymbol = sm.GetSymbolInfo(attributeSyntax, cancellationToken).Symbol as IMethodSymbol;
                                if (attrCtorSymbol == null || !tagNameAttribute.Equals(attrCtorSymbol.ContainingType, SymbolEqualityComparer.Default))
                                {
                                    // badly formed attribute definition, or not the right attribute
                                    continue;
                                }

                                bool hasMisconfiguredInput = false;
                                ImmutableArray<AttributeData>? boundAttributes = tagPropertySymbol?.GetAttributes();

                                if (boundAttributes == null)
                                {
                                    continue;
                                }

                                foreach (AttributeData attributeData in boundAttributes)
                                {
                                    // supports: [TagName("somename")]
                                    // supports: [TagName]
                                    if (attributeData.ConstructorArguments.Any())
                                    {
                                        foreach (TypedConstant typedConstant in attributeData.ConstructorArguments)
                                        {
                                            if (typedConstant.Kind == TypedConstantKind.Error)
                                            {
                                                hasMisconfiguredInput = true;
                                                break;
                                            }
                                        }

                                        if (hasMisconfiguredInput)
                                        {
                                            break;
                                        }

                                        ImmutableArray<TypedConstant> items = attributeData.ConstructorArguments;
                                        if (items.Length != 1)
                                        {
                                            hasMisconfiguredInput = true;
                                            break;
                                        }

                                        tagValue = (string?)items[0].Value;
                                        if (string.IsNullOrEmpty(tagValue))
                                        {
                                            reportDiagnostic(InvalidKeyDiagnostic.Create(attributeSyntax));
                                            hasMisconfiguredInput = true;
                                            break;
                                        }
                                    }
                                }

                                if (hasMisconfiguredInput)
                                {
                                    // skip further generator execution and let compiler generate the errors
                                    break;
                                }

                                properties ??= new List<PropertyTag>();
                                properties.Add(
                                    new PropertyTag(
                                        tagPropertySymbol!.IsReadOnly,
                                        propertyName: tagPropertySymbol.Name,
                                        tagValue!));
                            }
                        }
                    }

                    if (properties?.Count > 0)
                    {
                        results.Add(new TagList(
                                        GetClassNamespace(classDec),
                                        classDec.Identifier.ToString() + classDec.TypeParameterList,
                                        properties));
                    }
                }
            }

            return results;
        }

        private static string GetClassNamespace(ClassDeclarationSyntax classDec)
        {
            string? nameSpace;

            // determine the namespace the class is declared in, if any
            SyntaxNode? potentialNamespaceParent = classDec.Parent;
            while (potentialNamespaceParent != null &&
                   potentialNamespaceParent is not NamespaceDeclarationSyntax
                && potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
            {
                potentialNamespaceParent = potentialNamespaceParent.Parent;
            }

            if (potentialNamespaceParent is BaseNamespaceDeclarationSyntax namespaceParent)
            {
                nameSpace = namespaceParent.Name.ToString();
                while (true)
                {
                    if (namespaceParent.Parent is NamespaceDeclarationSyntax parent)
                    {
                        namespaceParent = parent;
                        nameSpace = $"{namespaceParent.Name}.{nameSpace}";
                    }
                    else
                    {
                        return nameSpace;
                    }
                }
            }

            return string.Empty;
        }

        internal readonly struct TagList
        {
            public readonly string Namespace;
            public readonly string ClassName;
            public readonly List<PropertyTag> Properties;

            public TagList(string nameSpace, string className, List<PropertyTag> properties)
            {
                Namespace = nameSpace;
                ClassName = className;
                Properties = properties;
            }
        }

        internal readonly struct PropertyTag
        {
            public readonly bool IsReadOnly;
            public readonly string PropertyName;
            public readonly string TagValue;

            public PropertyTag(bool isReadOnly, string propertyName, string tagValue)
            {
                IsReadOnly = isReadOnly;
                PropertyName = propertyName;
                TagValue = tagValue;
            }
        }
    }
}
