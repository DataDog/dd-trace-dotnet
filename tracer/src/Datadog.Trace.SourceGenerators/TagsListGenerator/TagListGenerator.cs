// <copyright file="TagListGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
            context.RegisterPostInitializationOutput(ctx => ctx.AddSource("TagAttribute.g.cs", Sources.Attributes));

            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations =
                context.SyntaxProvider.CreateSyntaxProvider(IsAttributedProperty, GetPotentialClassesForGeneration)
                       .Where(static m => m is not null)!;

            IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses =
                context.CompilationProvider.Combine(classDeclarations.Collect());

            IncrementalValueProvider<(IReadOnlyList<TagList>, IReadOnlyList<Diagnostic>)> tagListsAndDiagnostics =
                compilationAndClasses
                   .Select(static (t, ct) => GetTagLists(t.Item1, t.Item2, ct));

            context.RegisterSourceOutput(tagListsAndDiagnostics, static (spc, source) => Execute(source.Item1, source.Item2, spc));
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
                    if (attributeSyntax.Name.ToString() is "Tag" or "TagAttribute" or "Metric" or "MetricAttribute")
                    {
                        return propertyDeclarationSyntax.Parent as ClassDeclarationSyntax;
                    }
                }
            }

            return null;
        }

        private static void Execute(IReadOnlyList<TagList> tagLists, IReadOnlyList<Diagnostic> diagnostics, SourceProductionContext context)
        {
            foreach (var diagnostic in diagnostics)
            {
                context.ReportDiagnostic(diagnostic);
            }

            if (tagLists.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var tagList in tagLists)
                {
                    var source = Sources.CreateTagsList(sb, tagList);
                    context.AddSource($"{tagList.ClassName}.g.cs", SourceText.From(source, Encoding.UTF8));
                    sb.Clear();
                }
            }
        }

        private static (IReadOnlyList<TagList> TagLists, IReadOnlyList<Diagnostic> Diagnostics) GetTagLists(
            Compilation compilation,
            ImmutableArray<ClassDeclarationSyntax> classes,
            CancellationToken cancellationToken)
        {
            if (classes.IsDefaultOrEmpty)
            {
                // nothing to do yet
                return (Array.Empty<TagList>(), Array.Empty<Diagnostic>());
            }

            INamedTypeSymbol? tagAttribute = compilation.GetTypeByMetadataName(Constants.TagAttribute);
            INamedTypeSymbol? metricAttribute = compilation.GetTypeByMetadataName(Constants.MetricAttribute);
            if (tagAttribute is null || metricAttribute is null)
            {
                // nothing to do if these types aren't available
                return (Array.Empty<TagList>(), Array.Empty<Diagnostic>());
            }

            // get the double? return type
            INamedTypeSymbol nullableT = compilation.GetSpecialType(SpecialType.System_Nullable_T);
            INamedTypeSymbol tagReturnType = compilation.GetSpecialType(SpecialType.System_String);
            INamedTypeSymbol metricReturnType = nullableT.Construct(compilation.GetSpecialType(SpecialType.System_Double));

            List<Diagnostic>? diagnostics = null;
            var results = new List<TagList>();

            // we enumerate by syntax tree, to minimize the need to instantiate semantic models (since they're expensive)
            foreach (var group in classes.Distinct().GroupBy(x => x.SyntaxTree))
            {
                SemanticModel? sm = null;
                foreach (ClassDeclarationSyntax classDec in group)
                {
                    // stop if we're asked to
                    cancellationToken.ThrowIfCancellationRequested();

                    List<PropertyTag>? tagProperties = null;
                    List<PropertyTag>? metricProperties = null;

                    foreach (MemberDeclarationSyntax member in classDec.Members)
                    {
                        var property = member as PropertyDeclarationSyntax;
                        if (property is null)
                        {
                            // we only care about properties
                            continue;
                        }

                        sm ??= compilation.GetSemanticModel(classDec.SyntaxTree);
                        IPropertySymbol? propertySymbol = sm.GetDeclaredSymbol(property, cancellationToken) as IPropertySymbol;
                        Debug.Assert(propertySymbol is not null, "Tag/Metric property is not present");
                        INamedTypeSymbol? propertyReturnType = propertySymbol!.Type as INamedTypeSymbol;
                        string? key = null;

                        AttributeData? tagAttributeData = null;
                        AttributeData? metricAttributeData = null;

                        bool hasMisconfiguredInput = false;
                        ImmutableArray<AttributeData>? boundAttributes = propertySymbol.GetAttributes();

                        if (boundAttributes == null)
                        {
                            // no attributes, skip
                            continue;
                        }

                        foreach (AttributeData attributeData in boundAttributes)
                        {
                            var isTag = false;
                            var isMetric = false;
                            if (tagAttribute.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                            {
                                tagAttributeData = attributeData;
                                isTag = true;
                            }
                            else if (metricAttribute.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                            {
                                metricAttributeData = attributeData;
                                isMetric = true;
                            }
                            else
                            {
                                // Not the right attribute
                                continue;
                            }

                            if (tagAttributeData is not null && metricAttributeData is not null)
                            {
                                // can't have both!
                                hasMisconfiguredInput = true;
                                diagnostics ??= new List<Diagnostic>();
                                diagnostics.Add(DuplicateAttributeDiagnostic.Create(
                                                metricAttributeData.ApplicationSyntaxReference?.GetSyntax(),
                                                tagAttributeData.ApplicationSyntaxReference?.GetSyntax()));
                                break;
                            }

                            if (isTag && propertyReturnType is not null
                                      && !tagReturnType.Equals(propertyReturnType, SymbolEqualityComparer.Default))
                            {
                                diagnostics ??= new List<Diagnostic>();
                                diagnostics.Add(InvalidTagPropertyReturnTypeDiagnostic.Create(attributeData.ApplicationSyntaxReference?.GetSyntax()));
                                hasMisconfiguredInput = true;
                                break;
                            }

                            if (isMetric && propertyReturnType is not null
                                         && !metricReturnType.Equals(propertyReturnType, SymbolEqualityComparer.Default))
                            {
                                diagnostics ??= new List<Diagnostic>();
                                diagnostics.Add(InvalidMetricPropertyReturnTypeDiagnostic.Create(attributeData.ApplicationSyntaxReference?.GetSyntax()));
                                hasMisconfiguredInput = true;
                                break;
                            }

                            // Supports [Tag("some.tag")] or [Metric("some.metric")]
                            if (attributeData.ConstructorArguments.Length != 1)
                            {
                                hasMisconfiguredInput = true;
                                break;
                            }

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

                            key = (string?)attributeData.ConstructorArguments[0].Value;
                            if (string.IsNullOrEmpty(key))
                            {
                                diagnostics ??= new List<Diagnostic>();
                                diagnostics.Add(InvalidKeyDiagnostic.Create(attributeData.ApplicationSyntaxReference?.GetSyntax()));
                                hasMisconfiguredInput = true;
                                break;
                            }

                            if (key == "_dd.origin")
                            {
                                diagnostics ??= new List<Diagnostic>();
                                diagnostics.Add(InvalidUseOfOriginDiagnostic.Create(attributeData.ApplicationSyntaxReference?.GetSyntax()));
                                hasMisconfiguredInput = true;
                                break;
                            }

                            if (key == "language")
                            {
                                diagnostics ??= new List<Diagnostic>();
                                diagnostics.Add(InvalidUseOfLanguageDiagnostic.Create(attributeData.ApplicationSyntaxReference?.GetSyntax()));
                                hasMisconfiguredInput = true;
                                break;
                            }
                        }

                        if (hasMisconfiguredInput)
                        {
                            continue;
                        }

                        if (tagAttributeData is not null)
                        {
                            tagProperties ??= new List<PropertyTag>();
                            tagProperties.Add(
                                new PropertyTag(
                                    propertySymbol!.IsReadOnly,
                                    propertyName: propertySymbol.Name,
                                    key!));
                        }
                        else if (metricAttributeData is not null)
                        {
                            metricProperties ??= new List<PropertyTag>();
                            metricProperties.Add(
                                new PropertyTag(
                                    propertySymbol!.IsReadOnly,
                                    propertyName: propertySymbol.Name,
                                    key!));
                        }
                    }

                    if (tagProperties?.Count > 0 || metricProperties?.Count > 0)
                    {
                        results.Add(new TagList(
                                        GetClassNamespace(classDec),
                                        classDec.Identifier.ToString() + classDec.TypeParameterList,
                                        tagProperties,
                                        metricProperties));
                    }
                }
            }

            return (results, (IReadOnlyList<Diagnostic>?)diagnostics ?? Array.Empty<Diagnostic>());
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
            public readonly List<PropertyTag>? TagProperties;
            public readonly List<PropertyTag>? MetricProperties;

            public TagList(string nameSpace, string className, List<PropertyTag>? tagProperties, List<PropertyTag>? metricProperties)
            {
                Namespace = nameSpace;
                ClassName = className;
                TagProperties = tagProperties;
                MetricProperties = metricProperties;
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
