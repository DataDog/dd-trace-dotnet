// <copyright file="TelemetryMetricGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Text;
using System.Threading;
using Datadog.Trace.SourceGenerators.Helpers;
using Datadog.Trace.SourceGenerators.TelemetryMetric.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Datadog.Trace.SourceGenerators.TelemetryMetric
{
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
                       .Where(static m => m is not null)!;

            context.ReportDiagnostics(
                enums
                   .Where(static m => m.Errors.Count > 0)
                   .SelectMany(static (x, _) => x.Errors));

            context.RegisterSourceOutput(
                enums,
                static (spc, source) => Execute(source, spc));
        }

        private static void Execute(Result<(EnumDetails EnumDetails, bool IsValid)> result, SourceProductionContext spc)
        {
            if (result.Value.IsValid)
            {
                var enumDetails = result.Value.EnumDetails;
                var source = Sources.CreateMetricEnumExtension(new(), in enumDetails);
                spc.AddSource($"{enumDetails.ExtensionName}.g.cs", SourceText.From(source, Encoding.UTF8));
            }

            if (result.Errors.Count > 0)
            {
                foreach (var info in result.Errors)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(info.Descriptor, info.Location));
                }
            }
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
            bool hasMisconfiguredInput = false;
            List<DiagnosticInfo>? diagnostics = null;

            foreach (AttributeData attributeData in enumSymbol.GetAttributes())
            {
                if ((attributeData.AttributeClass?.Name == "TelemetryMetricTypeAttribute"
                  || attributeData.AttributeClass?.Name == "TelemetryMetricType")
                 && attributeData.AttributeClass.ToDisplayString() == TelemetryMetricTypeAttributeFullName
                 && attributeData.ConstructorArguments is { Length: 1 } args)
                {
                    metricType = args[0].Value is string s1
                                     ? s1
                                     : args[0].Value?.ToString();

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

            for (var i = 0; i < memberSymbols.Length; i++)
            {
                var memberSymbol = memberSymbols[i];
                if (memberSymbol is not IFieldSymbol field || field.ConstantValue is null)
                {
                    continue;
                }

                string? metricName = null;
                int? tagCount = null;
                bool isCommon = true;
                string? nameSpace = null;
                foreach (var attribute in memberSymbol.GetAttributes())
                {
                    if ((attribute.AttributeClass?.Name == "TelemetryMetricAttribute"
                      || attribute.AttributeClass?.Name == "TelemetryMetric")
                     && attribute.AttributeClass.ToDisplayString() == TelemetryMetricAttributeFullName
                     && attribute.ConstructorArguments is { Length: >= 2 and <= 4 } args)
                    {
                        metricName = args[0].Value?.ToString();
                        tagCount = args[1].Value as int?;
                        isCommon = true;
                        nameSpace = null;

                        if (args.Length > 2)
                        {
                            isCommon = args[2].Value as bool? ?? true;
                        }

                        if (args.Length > 3)
                        {
                            nameSpace = args[3].Value?.ToString();
                        }
                    }
                }

                if (string.IsNullOrEmpty(metricName) || !tagCount.HasValue)
                {
                    diagnostics ??= new List<DiagnosticInfo>();
                    diagnostics.Add(RequiredValuesMissingDiagnostic.CreateInfo(memberSymbol.DeclaringSyntaxReferences[0].GetSyntax()));
                    hasMisconfiguredInput = true;
                    continue;
                }

                members.Add((memberSymbol.Name, new MetricDetails(metricName!, tagCount.Value, isCommon, nameSpace)));
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

            string extensionName = enumSymbol.Name + "Extensions";
            string enumNameSpace = enumSymbol.ContainingNamespace.IsGlobalNamespace
                                       ? string.Empty
                                       : enumSymbol.ContainingNamespace.ToString();
            string fullyQualifiedName = enumSymbol.ToString();

            return new Result<(EnumDetails, bool)>(
                (new EnumDetails(
                     extensionName: extensionName,
                     ns: enumNameSpace,
                     fullyQualifiedName: fullyQualifiedName,
                     metricType: metricType!,
                     names: members.ToArray()), true),
                errors);
        }

        internal readonly record struct EnumDetails
        {
            public readonly string ExtensionName;
            public readonly string FullyQualifiedName;
            public readonly string MetricType;
            public readonly string Namespace;
            public readonly EquatableArray<(string Property, MetricDetails Value)> Names;

            public EnumDetails(
                string extensionName,
                string ns,
                string fullyQualifiedName,
                string metricType,
                (string Property, MetricDetails Value)[] names)
            {
                ExtensionName = extensionName;
                Namespace = ns;
                FullyQualifiedName = fullyQualifiedName;
                MetricType = metricType;
                Names = new EquatableArray<(string, MetricDetails)>(names);
            }
        }

        internal readonly record struct MetricDetails
        {
            public readonly string MetricName;
            public readonly int TagCount;
            public readonly bool IsCommon;
            public readonly string? NameSpace;

            public MetricDetails(string metricName, int tagCount, bool isCommon, string? nameSpace)
            {
                MetricName = metricName;
                TagCount = tagCount;
                IsCommon = isCommon;
                NameSpace = nameSpace;
            }
        }
    }
}
