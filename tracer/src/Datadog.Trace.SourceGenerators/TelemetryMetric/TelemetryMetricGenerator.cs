// <copyright file="TelemetryMetricGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.SourceGenerators.EnumExtensions.Diagnostics;
using Datadog.Trace.SourceGenerators.Helpers;
using Datadog.Trace.SourceGenerators.TelemetryMetric;
using Datadog.Trace.SourceGenerators.TelemetryMetric.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Source generator to apply to telemetry metric enums as part of telemetry metric implementation.
/// Lets you mark an enum value with <c>[TelemetryMetric]</c>, providing a metric name, whether it's
/// a "common" metric, and how many tags it should have. We use these to generate extension methods
/// used by the telemetry metrics implementation (similar to the generic enum source generator).
/// </summary>
[Generator]
public class TelemetryMetricGenerator : IIncrementalGenerator
{
    private const string TelemetryMetricTypeAttributeFullName = "Datadog.Trace.SourceGenerators.TelemetryMetricTypeAttribute";
    private const string TelemetryMetricAttributeFullName = "Datadog.Trace.SourceGenerators.TelemetryMetricAttribute";
    private const string DescriptionAttribute = "System.ComponentModel.DescriptionAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the attribute source
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource("TelemetryMetricAttribute.g.cs", Sources.Attributes));

        var enums =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                        TelemetryMetricTypeAttributeFullName,
                        static (node, _) => node is EnumDeclarationSyntax,
                        static (context, ct) => GetTypeToGenerate(context, ct))
                   .WithTrackingName(TrackingNames.PostTransform)
                   .Where(static m => m is not null)!;

        context.ReportDiagnostics(
            enums
               .Where(static m => m.Errors.Count > 0)
               .SelectMany(static (x, _) => x.Errors)
               .WithTrackingName(TrackingNames.Diagnostics));

        var validEnums = enums
                        .Where(static m => m.Value.IsValid)
                        .Select(static (x, _) => x.Value.EnumDetails)
                        .WithTrackingName(TrackingNames.ValidValues);

        context.RegisterSourceOutput(
            validEnums,
            static (spc, source) => GenerateEnumSpecificCollectors(in source, spc));

        context.RegisterSourceOutput(
            validEnums.Collect().WithTrackingName(TrackingNames.Collected),
            static (spc, source) => GenerateAggregateCollectors(in source, spc));
    }

    private static void GenerateEnumSpecificCollectors(in EnumDetails enumDetails, SourceProductionContext spc)
    {
        var sb = new StringBuilder();
        var enumDictionary = GetEnumDictionary(in enumDetails);
        var (metricsToLocation, entryCounts) = GetTranslationArrays(in enumDetails, enumDictionary);
        var enumSource = Sources.CreateMetricEnumExtension(sb, in enumDetails, enumDictionary);
        var collectorSource = enumDetails.MetricType switch
        {
            "count" => Sources.CreateCountTelemetryCollectorPartial(sb, in enumDetails, enumDictionary, metricsToLocation, entryCounts),
            "gauge" => Sources.CreateGaugeTelemetryCollectorPartial(sb, in enumDetails, enumDictionary, metricsToLocation, entryCounts),
            "distribution" => Sources.CreateDistributionTelemetryCollectorPartial(sb, in enumDetails, enumDictionary, metricsToLocation, entryCounts),
            _ => $"// Metric type: {enumDetails.MetricType} not currently supported",
        };
        var ciVisibilitySource = enumDetails.MetricType switch
        {
            "count" => Sources.CreateCountCiVisibilityTelemetryCollectorPartial(sb, in enumDetails, enumDictionary, metricsToLocation, entryCounts),
            "gauge" => Sources.CreateGaugeCiVisibilityTelemetryCollectorPartial(sb, in enumDetails, enumDictionary, metricsToLocation, entryCounts),
            "distribution" => Sources.CreateDistributionCiVisibilityTelemetryCollectorPartial(sb, in enumDetails, enumDictionary, metricsToLocation, entryCounts),
            _ => $"// Metric type: {enumDetails.MetricType} not currently supported",
        };

        var interfaceSource = enumDetails.MetricType switch
        {
            "count" => Sources.CreateCountITelemetryCollectorPartial(sb, in enumDetails, enumDictionary),
            "gauge" => Sources.CreateGaugeITelemetryCollectorPartial(sb, in enumDetails, enumDictionary),
            "distribution" => Sources.CreateDistributionITelemetryCollectorPartial(sb, in enumDetails, enumDictionary),
            _ => $"// Metric type: {enumDetails.MetricType} not currently supported",
        };
        var nullSource = enumDetails.MetricType switch
        {
            "count" => Sources.CreateCountNullTelemetryCollectorPartial(sb, in enumDetails, enumDictionary, metricsToLocation, entryCounts),
            "gauge" => Sources.CreateGaugeNullTelemetryCollectorPartial(sb, in enumDetails, enumDictionary, metricsToLocation, entryCounts),
            "distribution" => Sources.CreateDistributionNullTelemetryCollectorPartial(sb, in enumDetails, enumDictionary, metricsToLocation, entryCounts),
            _ => $"// Metric type: {enumDetails.MetricType} not currently supported",
        };

        spc.AddSource($"{enumDetails.ShortName}Extensions.g.cs", SourceText.From(enumSource, Encoding.UTF8));
        spc.AddSource($"MetricsTelemetryCollector_{enumDetails.ShortName}.g.cs", SourceText.From(collectorSource, Encoding.UTF8));
        spc.AddSource($"CiVisibilityMetricsTelemetryCollector_{enumDetails.ShortName}.g.cs", SourceText.From(ciVisibilitySource, Encoding.UTF8));
        spc.AddSource($"IMetricsTelemetryCollector_{enumDetails.ShortName}.g.cs", SourceText.From(interfaceSource, Encoding.UTF8));
        spc.AddSource($"NullMetricsTelemetryCollector_{enumDetails.ShortName}.g.cs", SourceText.From(nullSource, Encoding.UTF8));

        static Dictionary<string, EquatableArray<string>> GetEnumDictionary(in EnumDetails details)
        {
            var enumDictionary = new Dictionary<string, EquatableArray<string>>();
            if (details.TagValues.AsArray() is { } values)
            {
                foreach (var (tagType, members) in values)
                {
                    enumDictionary[tagType] = members;
                }
            }

            return enumDictionary;
        }

        static (int[] MetricToLocation, int[] EntryCounts) GetTranslationArrays(in EnumDetails enumDetails, Dictionary<string, EquatableArray<string>> enumDictionary)
        {
            var names = enumDetails.Names.AsArray();
            if (names is null)
            {
                return (Array.Empty<int>(), Array.Empty<int>());
            }

            var locations = new int[names.Length];
            var entryCounts = new int[names.Length];

            var index = 0;
            for (var i = 0; i < names.Length; i++)
            {
                locations[i] = index;
                var (_, metric) = names[i];
                var tag1Count = metric.Tag1FullyQualifiedName is { } tag1Type && enumDictionary[tag1Type].AsArray() is { } tag1Values
                                    ? tag1Values.Length
                                    : 1;

                var tag2Count = metric.Tag2FullyQualifiedName is { } tag2Type && enumDictionary[tag2Type].AsArray() is { } tag2Values
                                    ? tag2Values.Length
                                    : 1;

                var entryCount = tag1Count * tag2Count;
                entryCounts[i] = entryCount;
                index += entryCount;
            }

            return (locations, entryCounts);
        }
    }

    private static void GenerateAggregateCollectors(in ImmutableArray<EnumDetails> enumDetails, SourceProductionContext spc)
    {
        var sb = new StringBuilder();
        var collectorSource = Sources.CreateAggregateTelemetryCollectorPartial(sb, in enumDetails);
        var ciVisibilitySource = Sources.CreateAggregateCiVisibilityTelemetryCollectorPartial(sb, in enumDetails);

        spc.AddSource($"MetricsTelemetryCollector.g.cs", SourceText.From(collectorSource, Encoding.UTF8));
        spc.AddSource($"CiVisibilityMetricsTelemetryCollector.g.cs", SourceText.From(ciVisibilitySource, Encoding.UTF8));
    }

    private static Result<(EnumDetails EnumDetails, bool IsValid)> GetTypeToGenerate(
        GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        var enumSymbol = ctx.TargetSymbol as INamedTypeSymbol;
        if (enumSymbol is null)
        {
            // nothing to do if this type isn't available
            return new Result<(EnumDetails, bool)>((default, false), default);
        }

        ct.ThrowIfCancellationRequested();

        string? metricType = null;
        bool isCiAppMetric = false;
        bool isApmMetric = true;
        bool hasMisconfiguredInput = false;
        List<DiagnosticInfo>? diagnostics = null;

        foreach (AttributeData attributeData in enumSymbol.GetAttributes())
        {
            if ((attributeData.AttributeClass?.Name == "TelemetryMetricTypeAttribute"
              || attributeData.AttributeClass?.Name == "TelemetryMetricType")
             && attributeData.AttributeClass.ToDisplayString() == TelemetryMetricTypeAttributeFullName)
            {
                if (attributeData.ConstructorArguments is { Length: >= 1 } args)
                {
                    metricType = args[0].Value as string ?? args[0].Value?.ToString();
                }

                if (attributeData.ConstructorArguments is { Length: 3 } multiArgs)
                {
                    isCiAppMetric = (args[1].Value as bool?) ?? false;
                    isApmMetric = (args[2].Value as bool?) ?? true;
                }

                if (string.IsNullOrEmpty(metricType))
                {
                    diagnostics ??= new List<DiagnosticInfo>();
                    diagnostics.Add(MissingMetricTypeDiagnostic.CreateInfo(attributeData.ApplicationSyntaxReference?.GetSyntax()));
                    hasMisconfiguredInput = true;
                }
            }
        }

        ct.ThrowIfCancellationRequested();
        var memberSymbols = enumSymbol.GetMembers();
        var members = new List<(string, MetricDetails)>(memberSymbols.Length);
        var uniqueValues = new HashSet<string>();
        var enumTypeDictionary = new Dictionary<string, EquatableArray<string>>();

        for (var i = 0; i < memberSymbols.Length; i++)
        {
            var memberSymbol = memberSymbols[i];
            if (memberSymbol is not IFieldSymbol field || field.ConstantValue is null)
            {
                continue;
            }

            string? metricName = null;
            bool isCommon = true;
            string? nameSpace = null;
            string? tag1FullyQualifiedName = null;
            string? tag2FullyQualifiedName = null;
            foreach (var attribute in memberSymbol.GetAttributes())
            {
                if (attribute.AttributeClass?.Name is "TelemetryMetricAttribute" or "TelemetryMetric"
                 && attribute.ConstructorArguments is { Length: >= 1 and <= 3 } args)
                {
                    metricName = args[0].Value?.ToString();
                    isCommon = true;
                    nameSpace = null;

                    if (args.Length > 1)
                    {
                        isCommon = args[1].Value as bool? ?? true;
                    }

                    if (args.Length > 2)
                    {
                        nameSpace = args[2].Value?.ToString();
                    }

                    var tagCount = attribute.AttributeClass.TypeParameters.Length;
                    if (tagCount > 0)
                    {
                        var tag1TypeParameter = attribute.AttributeClass.TypeArguments[0];
                        tag1FullyQualifiedName = tag1TypeParameter.ToString();
                        if (!enumTypeDictionary.ContainsKey(tag1FullyQualifiedName))
                        {
                            enumTypeDictionary[tag1FullyQualifiedName] = GetTagValues(tag1TypeParameter, ref diagnostics);
                        }
                    }

                    if (tagCount == 2)
                    {
                        var tag2TypeParameter = attribute.AttributeClass.TypeArguments[1];
                        tag2FullyQualifiedName = tag2TypeParameter.ToString();
                        if (!enumTypeDictionary.ContainsKey(tag2FullyQualifiedName))
                        {
                            enumTypeDictionary[tag2FullyQualifiedName] = GetTagValues(tag2TypeParameter, ref diagnostics);
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(metricName))
            {
                diagnostics ??= new List<DiagnosticInfo>();
                diagnostics.Add(RequiredValuesMissingDiagnostic.CreateInfo(memberSymbol.DeclaringSyntaxReferences[0].GetSyntax()));
                hasMisconfiguredInput = true;
                continue;
            }

            members.Add((memberSymbol.Name, new MetricDetails(metricName!, isCommon, nameSpace, tag1FullyQualifiedName, tag2FullyQualifiedName)));
            if (!uniqueValues.Add($"{metricName} {nameSpace ?? string.Empty} {(isCommon ? "true" : "false")}"))
            {
                diagnostics ??= new List<DiagnosticInfo>();
                diagnostics.Add(DuplicateMetricValueDiagnostic.CreateInfo(memberSymbol.DeclaringSyntaxReferences[0].GetSyntax()));
                // don't mark as misconfigured, as doesn't impact the generation of the extension methods
            }
        }

        var errors = diagnostics is { Count: > 0 }
                         ? new EquatableArray<DiagnosticInfo>(diagnostics.ToArray())
                         : default;

        if (hasMisconfiguredInput)
        {
            return new Result<(EnumDetails, bool)>((default, false), errors);
        }

        string enumNameSpace = enumSymbol.ContainingNamespace.IsGlobalNamespace
                                   ? string.Empty
                                   : enumSymbol.ContainingNamespace.ToString();
        string fullyQualifiedName = enumSymbol.ToString();

        var entries = new (string, EquatableArray<string>)[enumTypeDictionary.Count];
        var keyNo = 0;
        foreach (var kvp in enumTypeDictionary)
        {
            entries[keyNo] = (kvp.Key, kvp.Value);
            keyNo++;
        }

        return new Result<(EnumDetails, bool)>(
            (new EnumDetails(
                 shortName: enumSymbol.Name,
                 ns: enumNameSpace,
                 fullyQualifiedName: fullyQualifiedName,
                 metricType: metricType!,
                 isCiAppMetric: isCiAppMetric,
                 isApmMetric: isApmMetric,
                 names: new(members.ToArray()),
                 tagValues: new(entries)), true),
            errors);
    }

    private static EquatableArray<string> GetTagValues(ITypeSymbol enumSymbol, ref List<DiagnosticInfo>? diagnostics)
    {
        var enumMembers = enumSymbol.GetMembers();
        HashSet<string> descriptions = new();

        foreach (var member in enumMembers)
        {
            if (member is not IFieldSymbol field || field.ConstantValue is null)
            {
                continue;
            }

            foreach (var attribute in member.GetAttributes())
            {
                if (attribute.AttributeClass?.Name is "DescriptionAttribute" or "Description"
                 && attribute.AttributeClass.ToDisplayString() == DescriptionAttribute
                 && attribute.ConstructorArguments.Length == 1)
                {
                    if (attribute.ConstructorArguments[0].Value?.ToString() is { } dn)
                    {
                        if (!descriptions.Add(dn))
                        {
                            diagnostics ??= new List<DiagnosticInfo>();
                            diagnostics.Add(DuplicateDescriptionDiagnostic.CreateInfo(attribute.ApplicationSyntaxReference?.GetSyntax()));
                        }

                        break;
                    }

                    descriptions.Add(member.Name);
                }
            }
        }

        return descriptions.Count == 0
                   ? default
                   : new EquatableArray<string>(descriptions.ToArray());
    }

    internal readonly record struct EnumDetails
    {
        public readonly string ShortName;
        public readonly string FullyQualifiedName;
        public readonly string MetricType;
        public readonly bool IsCiAppMetric;
        public readonly bool IsApmMetric;
        public readonly string Namespace;
        public readonly EquatableArray<(string Property, MetricDetails Value)> Names;
        public readonly EquatableArray<(string TagType, EquatableArray<string> Entries)> TagValues;

        public EnumDetails(string shortName, string fullyQualifiedName, string metricType, bool isCiAppMetric, bool isApmMetric, string ns, EquatableArray<(string Property, MetricDetails Value)> names, EquatableArray<(string TagType, EquatableArray<string> Entries)> tagValues)
        {
            ShortName = shortName;
            FullyQualifiedName = fullyQualifiedName;
            MetricType = metricType;
            Namespace = ns;
            Names = names;
            TagValues = tagValues;
            IsCiAppMetric = isCiAppMetric;
            IsApmMetric = isApmMetric;
        }
    }

    internal readonly record struct MetricDetails
    {
        public readonly string MetricName;
        public readonly bool IsCommon;
        public readonly string? NameSpace;
        public readonly string? Tag1FullyQualifiedName;
        public readonly string? Tag2FullyQualifiedName;

        public MetricDetails(string metricName, bool isCommon, string? nameSpace, string? tag1FullyQualifiedName, string? tag2FullyQualifiedName)
        {
            MetricName = metricName;
            IsCommon = isCommon;
            NameSpace = nameSpace;
            Tag1FullyQualifiedName = tag1FullyQualifiedName;
            Tag2FullyQualifiedName = tag2FullyQualifiedName;
        }
    }
}
