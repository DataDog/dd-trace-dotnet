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
using Datadog.Trace.SourceGenerators.Helpers;
using Datadog.Trace.SourceGenerators.TagsListGenerator;
using Datadog.Trace.SourceGenerators.TagsListGenerator.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <inheritdoc />
[Generator]
public class TagListGenerator : IIncrementalGenerator
{
    private const string TagAttributeFullName = "Datadog.Trace.SourceGenerators.TagAttribute";
    private const string MetricAttributeFullName = "Datadog.Trace.SourceGenerators.MetricAttribute";

    internal enum PropertyType
    {
        String,
        NullableInt,
        NullableDouble,
    }

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the attribute source
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource("TagAttribute.g.cs", Sources.Attributes));

        var tagProperties =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                        TagAttributeFullName,
                        static (node, _) => node is PropertyDeclarationSyntax,
                        static (context, ct) => GetPropertyTagForTags(context, ct))
                   .Where(static m => m is not null)!
                   .WithTrackingName(TrackingNames.TagResults);

        var metricProperties =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                        MetricAttributeFullName,
                        static (node, _) => node is PropertyDeclarationSyntax,
                        static (context, ct) => GetPropertyTagForMetrics(context, ct))
                   .Where(static m => m is not null)!
                   .WithTrackingName(TrackingNames.MetricResults);

        context.ReportDiagnostics(
            tagProperties
               .Where(static m => m.Errors.Count > 0)
               .SelectMany(static (x, _) => x.Errors)
               .WithTrackingName(TrackingNames.TagDiagnostics));

        context.ReportDiagnostics(
            metricProperties
               .Where(static m => m.Errors.Count > 0)
               .SelectMany(static (x, _) => x.Errors)
               .WithTrackingName(TrackingNames.MetricDiagnostics));

        var allTags = tagProperties
                     .Where(static m => m.Value.IsValid)
                     .Select(static (x, _) => x.Value.PropertyTag)
                     .Collect()
                     .WithTrackingName(TrackingNames.AllTags);

        var allMetrics = metricProperties
                        .Where(static m => m.Value.IsValid)
                        .Select(static (x, _) => x.Value.PropertyTag)
                        .Collect()
                        .WithTrackingName(TrackingNames.AllMetrics);

        var allProperties = allTags
                           .Combine(allMetrics)
                           .WithTrackingName(TrackingNames.AllProperties);

        context.RegisterSourceOutput(
            allProperties,
            static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static void Execute(
        ImmutableArray<PropertyTag> tagProperties,
        ImmutableArray<PropertyTag> metricProperties,
        SourceProductionContext context)
    {
        if (tagProperties.IsDefaultOrEmpty && metricProperties.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return;
        }

        StringBuilder? sb = null;
        foreach (var tagList in GetTagLists(tagProperties, metricProperties))
        {
            sb ??= new StringBuilder();
            var source = Sources.CreateTagsList(sb, tagList);
            context.AddSource($"{tagList.ClassName}.g.cs", SourceText.From(source, Encoding.UTF8));
            sb.Clear();
        }
    }

    private static IEnumerable<TagList> GetTagLists(ImmutableArray<PropertyTag> tagProperties, ImmutableArray<PropertyTag> metricProperties)
    {
        return tagProperties
              .Concat(metricProperties)
              .GroupBy(x => (x.ClassName, x.Namespace))
              .Select(
                   @grp =>
                   {
                       var tags = @grp.Where(x => x.IsTag).ToImmutableArray();
                       var metrics = @grp.Where(x => !x.IsTag).ToImmutableArray();

                       return new TagList(grp.Key.Namespace, grp.Key.ClassName, tags, metrics);
                   });
    }

    private static Result<(PropertyTag PropertyTag, bool IsValid)> GetPropertyTagForTags(
        GeneratorAttributeSyntaxContext ctx, CancellationToken ct) =>
        GetPropertyTag(ctx, ct, "TagAttribute", "Tag", TagAttributeFullName, true);

    private static Result<(PropertyTag PropertyTag, bool IsValid)> GetPropertyTagForMetrics(
        GeneratorAttributeSyntaxContext ctx, CancellationToken ct) =>
        GetPropertyTag(ctx, ct, "MetricAttribute", "Metric", MetricAttributeFullName, false);

    private static Result<(PropertyTag PropertyTag, bool IsValid)> GetPropertyTag(
        GeneratorAttributeSyntaxContext ctx, CancellationToken ct, string partialTagName1, string partialTagName2, string fullTagName, bool isTag)
    {
        var property = (PropertyDeclarationSyntax)ctx.TargetNode;
        var classDec = (ClassDeclarationSyntax)ctx.TargetNode.Parent!;
        var propertySymbol = ctx.SemanticModel.GetDeclaredSymbol(property, ct) as IPropertySymbol;
        List<DiagnosticInfo>? diagnostics = null;
        bool hasMisconfiguredInput = false;
        string? key = null;
        string? otelKey = null;

        foreach (AttributeData attributeData in propertySymbol!.GetAttributes())
        {
            if ((attributeData.AttributeClass?.Name == partialTagName1 ||
                 attributeData.AttributeClass?.Name == partialTagName2)
             && attributeData.AttributeClass.ToDisplayString() == fullTagName)
            {
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
                    diagnostics ??= new List<DiagnosticInfo>();
                    diagnostics.Add(InvalidKeyDiagnostic.CreateInfo(attributeData.ApplicationSyntaxReference?.GetSyntax()));
                    hasMisconfiguredInput = true;
                    break;
                }

                if (key == "_dd.origin")
                {
                    diagnostics ??= new List<DiagnosticInfo>();
                    diagnostics.Add(InvalidUseOfOriginDiagnostic.CreateInfo(attributeData.ApplicationSyntaxReference?.GetSyntax()));
                    hasMisconfiguredInput = true;
                    break;
                }

                if (key == "language")
                {
                    diagnostics ??= new List<DiagnosticInfo>();
                    diagnostics.Add(InvalidUseOfLanguageDiagnostic.CreateInfo(attributeData.ApplicationSyntaxReference?.GetSyntax()));
                    hasMisconfiguredInput = true;
                    break;
                }

                if (isTag)
                {
                    foreach (var namedArgument in attributeData.NamedArguments)
                    {
                        if (namedArgument.Key != "OTelName")
                        {
                            continue;
                        }

                        if (namedArgument.Value.Kind == TypedConstantKind.Error)
                        {
                            hasMisconfiguredInput = true;
                            break;
                        }

                        otelKey = (string?)namedArgument.Value.Value;
                        if (string.IsNullOrEmpty(otelKey))
                        {
                            diagnostics ??= new List<DiagnosticInfo>();
                            diagnostics.Add(InvalidKeyDiagnostic.CreateInfo(attributeData.ApplicationSyntaxReference?.GetSyntax()));
                            hasMisconfiguredInput = true;
                            break;
                        }

                        if (otelKey == "_dd.origin")
                        {
                            diagnostics ??= new List<DiagnosticInfo>();
                            diagnostics.Add(InvalidUseOfOriginDiagnostic.CreateInfo(attributeData.ApplicationSyntaxReference?.GetSyntax()));
                            hasMisconfiguredInput = true;
                            break;
                        }

                        if (otelKey == "language")
                        {
                            diagnostics ??= new List<DiagnosticInfo>();
                            diagnostics.Add(InvalidUseOfLanguageDiagnostic.CreateInfo(attributeData.ApplicationSyntaxReference?.GetSyntax()));
                            hasMisconfiguredInput = true;
                            break;
                        }
                    }
                }
            }
        }

        PropertyType propertyType = PropertyType.String;
        bool hasRequiredReturnType = false;
        if (isTag)
        {
            if (propertySymbol.Type.Name == "String")
            {
                propertyType = PropertyType.String;
                hasRequiredReturnType = true;
            }
            else if (propertySymbol.Type is INamedTypeSymbol { Name: "Nullable", TypeArguments: { Length: 1 } intTypeArgs }
                  && intTypeArgs[0].Name == "Int32")
            {
                propertyType = PropertyType.NullableInt;
                hasRequiredReturnType = true;
            }
        }
        else if (propertySymbol.Type is INamedTypeSymbol { Name: "Nullable", TypeArguments: { Length: 1 } doubleTypeArgs }
              && doubleTypeArgs[0].Name == "Double")
        {
            propertyType = PropertyType.NullableDouble;
            hasRequiredReturnType = true;
        }

        if (!hasRequiredReturnType)
        {
            hasMisconfiguredInput = true;
            diagnostics ??= new List<DiagnosticInfo>();
            diagnostics.Add(
                isTag
                    ? InvalidTagPropertyReturnTypeDiagnostic.CreateInfo(ctx.TargetNode)
                    : InvalidMetricPropertyReturnTypeDiagnostic.CreateInfo(ctx.TargetNode));
        }

        var errors = diagnostics is { Count: > 0 }
                         ? new EquatableArray<DiagnosticInfo>(diagnostics.ToArray())
                         : default;

        if (hasMisconfiguredInput)
        {
            return new Result<(PropertyTag PropertyTag, bool IsValid)>((default, false), errors);
        }

        var tag = new PropertyTag(
            nameSpace: GetClassNamespace(classDec),
            className: classDec.Identifier.ToString() + classDec.TypeParameterList,
            isReadOnly: propertySymbol!.IsReadOnly,
            propertyName: propertySymbol.Name,
            tagValue: key!,
            otelTagValue: otelKey,
            isTag: isTag,
            propertyType: propertyType);

        return new Result<(PropertyTag PropertyTag, bool IsValid)>((tag, true), errors);
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
        public readonly ImmutableArray<PropertyTag> TagProperties;
        public readonly ImmutableArray<PropertyTag> MetricProperties;

        public TagList(string nameSpace, string className, ImmutableArray<PropertyTag> tagProperties, ImmutableArray<PropertyTag> metricProperties)
        {
            Namespace = nameSpace;
            ClassName = className;
            TagProperties = tagProperties;
            MetricProperties = metricProperties;
        }
    }

    internal readonly record struct PropertyTag
    {
        public readonly string Namespace;
        public readonly string ClassName;
        public readonly bool IsReadOnly;
        public readonly string PropertyName;
        public readonly string TagValue;
        public readonly string? OTelTagValue;
        public readonly bool IsTag;
        public readonly PropertyType PropertyType;

        public PropertyTag(string nameSpace, string className, bool isReadOnly, string propertyName, string tagValue, string? otelTagValue, bool isTag, PropertyType propertyType)
        {
            IsReadOnly = isReadOnly;
            PropertyName = propertyName;
            TagValue = tagValue;
            OTelTagValue = otelTagValue;
            IsTag = isTag;
            Namespace = nameSpace;
            ClassName = className;
            PropertyType = propertyType;
        }
    }
}
