// <copyright file="PublicApiGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.SourceGenerators.Helpers;
using Datadog.Trace.SourceGenerators.PublicApi;
using Datadog.Trace.SourceGenerators.PublicApi.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Source generator that creates instrumented public properties from fields decorated with <c>[GeneratePublicApi]</c>.
/// </summary>
[Generator]
public class PublicApiGenerator : IIncrementalGenerator
{
    private const string GeneratePublicApiAttribute = "Datadog.Trace.SourceGenerators.GeneratePublicApiAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the attribute source
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource("GeneratePublicApiAttribute.g.cs", Sources.Attributes));

        // Expected syntax
        // FieldDeclaration
        //  - VariableDeclaration
        //    - VariableDeclaration
        var properties =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                        GeneratePublicApiAttribute,
                        static (node, _) => node is PropertyDeclarationSyntax,
                        static (context, ct) => GetPublicApiProperties(context, ct))
                   .Where(static m => m is not null)!
                   .WithTrackingName(TrackingNames.PostTransform);

        context.ReportDiagnostics(
            properties
               .Where(static m => m.Errors.Count > 0)
               .SelectMany(static (x, _) => x.Errors)
               .WithTrackingName(TrackingNames.Diagnostics));

        var allValidProperties = properties
                     .Where(static m => m.Value.IsValid)
                     .Select(static (x, _) => x.Value.PropertyTag)
                     .WithTrackingName(TrackingNames.ValidValues)
                     .Collect();

        context.RegisterSourceOutput(allValidProperties, Execute);
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<PublicApiProperty> properties)
    {
        if (properties.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return;
        }

        var sb = new StringBuilder();
        foreach (var partialClass in properties.GroupBy(x => (x.ClassName, x.Namespace, x.IsRecord)))
        {
            sb.Clear();

            var (className, nameSpace, isRecord) = partialClass.Key;
            var source = Sources.CreatePartialClass(sb, nameSpace, className, isRecord, partialClass);
            context.AddSource($"{nameSpace}.{className}.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private static Result<(PublicApiProperty PropertyTag, bool IsValid)> GetPublicApiProperties(
        GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        var property = (PropertyDeclarationSyntax)ctx.TargetNode;
        var classDec = ctx.TargetNode.Parent as TypeDeclarationSyntax;

        bool isReferenceType;

        if (classDec is RecordDeclarationSyntax recordDeclaration)
        {
            var keyword = recordDeclaration.ClassOrStructKeyword.Text;
            isReferenceType = string.IsNullOrEmpty(keyword) || keyword == "class";
        }
        else
        {
            isReferenceType = classDec is ClassDeclarationSyntax;
        }

        if (classDec is null || !isReferenceType)
        {
            // only support properties on classes
            return new Result<(PublicApiProperty PropertyTag, bool IsValid)>(
                (default, false),
                new EquatableArray<DiagnosticInfo>(new[] { OnlySupportsClassesDiagnostic.CreateInfo(property) }));
        }

        var propertySymbol = ctx.TargetSymbol as IPropertySymbol;
        if (propertySymbol is null)
        {
            // something weird going on
            return new Result<(PublicApiProperty PropertyTag, bool IsValid)>((default, false), default);
        }

        List<DiagnosticInfo>? diagnostics = null;
        bool hasMisconfiguredInput = false;
        int? publicApiGetter = null;
        int? publicApiSetter = null;
        string? obsoleteMessage = null;

        foreach (AttributeData attributeData in propertySymbol.GetAttributes())
        {
            if ((attributeData.AttributeClass?.Name == "GeneratePublicApiAttribute" ||
                 attributeData.AttributeClass?.Name == "GeneratePublicApi")
             && attributeData.AttributeClass.ToDisplayString() == GeneratePublicApiAttribute)
            {
                var args = attributeData.ConstructorArguments;
                if (args.Length == 0)
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

                publicApiGetter = args[0].Value as int?;

                if (args.Length > 1)
                {
                    publicApiSetter = args[1].Value as int?;
                    if (propertySymbol.IsReadOnly && publicApiSetter.HasValue)
                    {
                        diagnostics ??= new List<DiagnosticInfo>();
                        diagnostics.Add(SetterOnReadonlyFieldDiagnostic.CreateInfo(attributeData.ApplicationSyntaxReference?.GetSyntax()));
                        hasMisconfiguredInput = true;
                    }
                }
            }
            else if (attributeData.AttributeClass?.ToDisplayString() == "System.ObsoleteAttribute")
            {
                var args = attributeData.ConstructorArguments;
                if (args.Length == 0)
                {
                    obsoleteMessage = string.Empty;
                    continue;
                }

                foreach (TypedConstant typedConstant in args)
                {
                    if (typedConstant.Kind == TypedConstantKind.Error)
                    {
                        hasMisconfiguredInput = true;
                        break;
                    }
                }

                obsoleteMessage = args[0].Value as string;
            }
        }

        var fieldName = propertySymbol.Name;
        var propertyName = GetCalculatedPropertyName(fieldName);
        if (string.IsNullOrEmpty(propertyName))
        {
            diagnostics ??= new List<DiagnosticInfo>();
            diagnostics.Add(NamingProblemDiagnostic.CreateInfo(property));
            hasMisconfiguredInput = true;
        }

        if (!classDec.Modifiers.Any(x => x.IsKind(SyntaxKind.PartialKeyword)))
        {
            diagnostics ??= new List<DiagnosticInfo>();
            diagnostics.Add(PartialModifierIsRequiredDiagnostic.CreateInfo(property));
        }

        var errors = diagnostics is { Count: > 0 }
                         ? new EquatableArray<DiagnosticInfo>(diagnostics.ToArray())
                         : default;

        if (hasMisconfiguredInput)
        {
            return new Result<(PublicApiProperty PropertyTag, bool IsValid)>((default, false), errors);
        }

        var tag = new PublicApiProperty(
            nameSpace: GetClassNamespace(classDec),
            className: classDec.Identifier.ToString() + classDec.TypeParameterList,
            isRecord: classDec is RecordDeclarationSyntax,
            fieldName: fieldName,
            propertyName: propertyName!,
            publicApiGetter: publicApiGetter,
            publicApiSetter: publicApiSetter,
            returnType: propertySymbol.Type.ToDisplayString(),
            leadingTrivia: property.GetLeadingTrivia().ToFullString(),
            obsoleteMessage: obsoleteMessage);

        return new Result<(PublicApiProperty PropertyTag, bool IsValid)>((tag, true), errors);
    }

    private static string? GetCalculatedPropertyName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            return null;
        }

        if (fieldName[0] == '_')
        {
            return fieldName.Substring(1);
        }

        if (fieldName.EndsWith("Internal"))
        {
            return fieldName.Substring(0, fieldName.Length - 8);
        }

        return null;
    }

    private static string GetClassNamespace(TypeDeclarationSyntax classDec)
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

    internal readonly struct PartialClasses
    {
        public readonly string Namespace;
        public readonly string ClassName;
        public readonly ImmutableArray<PublicApiProperty> TagProperties;
        public readonly ImmutableArray<PublicApiProperty> MetricProperties;

        public PartialClasses(string nameSpace, string className, ImmutableArray<PublicApiProperty> tagProperties, ImmutableArray<PublicApiProperty> metricProperties)
        {
            Namespace = nameSpace;
            ClassName = className;
            TagProperties = tagProperties;
            MetricProperties = metricProperties;
        }
    }

    internal readonly record struct PublicApiProperty
    {
        public readonly string Namespace;
        public readonly string ClassName;
        public readonly bool IsRecord;
        public readonly string FieldName;
        public readonly int? PublicApiGetter;
        public readonly int? PublicApiSetter;
        public readonly string PropertyName;
        public readonly string ReturnType;
        public readonly string LeadingTrivia;
        public readonly string? ObsoleteMessage;

        public PublicApiProperty(string nameSpace, string className, bool isRecord, string fieldName, int? publicApiGetter, int? publicApiSetter, string propertyName, string returnType, string leadingTrivia, string? obsoleteMessage)
        {
            Namespace = nameSpace;
            ClassName = className;
            IsRecord = isRecord;
            FieldName = fieldName;
            PublicApiGetter = publicApiGetter;
            PublicApiSetter = publicApiSetter;
            PropertyName = propertyName;
            ReturnType = returnType;
            LeadingTrivia = leadingTrivia;
            ObsoleteMessage = obsoleteMessage;
        }
    }
}
