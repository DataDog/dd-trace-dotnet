// <copyright file="Sources.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Datadog.Trace.SourceGenerators.Helpers;

namespace Datadog.Trace.SourceGenerators.TelemetryMetric;

internal partial class Sources
{
    private enum CollectorType
    {
        Null,
        Apm,
        Ci,
    }

    public static string CreateMetricEnumExtension(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary)
    {
        return Constants.FileHeader + $$"""
            namespace {{details.Namespace}};
            internal static partial class {{details.ShortName}}Extensions
            {
                /// <summary>
                /// The number of separate metrics in the <see cref="{{details.FullyQualifiedName}}" /> metric.
                /// </summary>
                public const int Length = {{details.Names.Count}};

                /// <summary>
                /// Gets the metric name for the provided metric
                /// </summary>
                /// <param name="metric">The metric to get the name for</param>
                /// <returns>The datadog metric name</returns>
                public static string GetName(this {{details.FullyQualifiedName}} metric)
                    => metric switch
                    {{{GetNames(sb, in details)}}
                        _ => null!,
                    };

                /// <summary>
                /// Gets whether the metric is a "common" metric, used by all tracers
                /// </summary>
                /// <param name="metric">The metric to check</param>
                /// <returns>True if the metric is a "common" metric, used by all languages</returns>
                public static bool IsCommon(this {{details.FullyQualifiedName}} metric)
                    => metric switch
                    {{{GetIsCommon(sb, in details)}}
                        _ => true,
                    };

                /// <summary>
                /// Gets the custom namespace for the provided metric
                /// </summary>
                /// <param name="metric">The metric to get the name for</param>
                /// <returns>The datadog metric name</returns>
                public static string? GetNamespace(this {{details.FullyQualifiedName}} metric)
                    => metric switch
                    {{{GetNamespaces(sb, in details)}}
                        _ => null,
                    };
            }
            """;
    }

    public static string CreateCountTelemetryCollectorPartial(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary, int[] metricsToLocation, int[] entryCounts)
        => CreateCountCollectorPartial(sb, CollectorType.Apm, in details, enumDictionary, metricsToLocation, entryCounts);

    public static string CreateCountCiVisibilityTelemetryCollectorPartial(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary, int[] metricsToLocation, int[] entryCounts)
        => CreateCountCollectorPartial(sb, CollectorType.Ci, in details, enumDictionary, metricsToLocation, entryCounts);

    public static string CreateCountNullTelemetryCollectorPartial(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary, int[] metricsToLocation, int[] entryCounts)
        => CreateCountCollectorPartial(sb, CollectorType.Null, in details, enumDictionary, metricsToLocation, entryCounts);

    public static string CreateCountITelemetryCollectorPartial(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary)
    {
        sb.Clear();
        sb.Append(Constants.FileHeader);
        sb.Append(
            """
            namespace Datadog.Trace.Telemetry;
            internal partial interface IMetricsTelemetryCollector
            {
            """);

        if (details.Names.AsArray() is { } names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                sb.AppendLine();
                var (property, metric) = names[i];

                sb.Append(
                    $$"""
                          public void Record{{details.ShortName}}{{property}}(
                      """);

                var array = metric.TagFullyQualifiedNames.AsArray() ?? [];
                WriteTagArgList(sb, array);

                sb.AppendLine("int increment = 1);");
            }
        }

        sb.Append("}");
        return sb.ToString();
    }

    public static string CreateGaugeTelemetryCollectorPartial(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary, int[] metricsToLocation, int[] entryCounts)
        => CreateGaugeCollectorPartial(sb, CollectorType.Apm, in details, enumDictionary, metricsToLocation, entryCounts);

    public static string CreateGaugeCiVisibilityTelemetryCollectorPartial(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary, int[] metricsToLocation, int[] entryCounts)
        => CreateGaugeCollectorPartial(sb, CollectorType.Ci, in details, enumDictionary, metricsToLocation, entryCounts);

    public static string CreateGaugeNullTelemetryCollectorPartial(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary, int[] metricsToLocation, int[] entryCounts)
        => CreateGaugeCollectorPartial(sb, CollectorType.Null, in details, enumDictionary, metricsToLocation, entryCounts);

    public static string CreateGaugeITelemetryCollectorPartial(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary)
    {
        sb.Clear();
        sb.Append(Constants.FileHeader);
        sb.Append(
            """
            namespace Datadog.Trace.Telemetry;
            internal partial interface IMetricsTelemetryCollector
            {
            """);

        if (details.Names.AsArray() is { } names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                sb.AppendLine();
                var (property, metric) = names[i];

                sb.Append(
                    $$"""
                          public void Record{{details.ShortName}}{{property}}(
                      """);

                var array = metric.TagFullyQualifiedNames.AsArray() ?? [];
                WriteTagArgList(sb, array);

                sb.AppendLine("int value);");
            }
        }

        sb.Append("}");
        return sb.ToString();
    }

    public static string CreateDistributionTelemetryCollectorPartial(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary, int[] metricsToLocation, int[] entryCounts)
        => CreateDistributionCollectorPartial(sb, CollectorType.Apm, in details, enumDictionary, metricsToLocation, entryCounts);

    public static string CreateDistributionCiVisibilityTelemetryCollectorPartial(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary, int[] metricsToLocation, int[] entryCounts)
        => CreateDistributionCollectorPartial(sb, CollectorType.Ci, in details, enumDictionary, metricsToLocation, entryCounts);

    public static string CreateDistributionNullTelemetryCollectorPartial(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary, int[] metricsToLocation, int[] entryCounts)
        => CreateDistributionCollectorPartial(sb, CollectorType.Null, in details, enumDictionary, metricsToLocation, entryCounts);

    public static string CreateDistributionITelemetryCollectorPartial(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary)
    {
        sb.Clear();
        sb.Append(Constants.FileHeader);
        sb.Append(
            """
            namespace Datadog.Trace.Telemetry;
            internal partial interface IMetricsTelemetryCollector
            {
            """);

        if (details.Names.AsArray() is { } names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                sb.AppendLine();
                var (property, metric) = names[i];

                sb.Append(
                    $$"""
                          public void Record{{details.ShortName}}{{property}}(
                      """);

                var array = metric.TagFullyQualifiedNames.AsArray() ?? [];
                WriteTagArgList(sb, array);

                sb.AppendLine("double value);");
            }
        }

        sb.Append("}");
        return sb.ToString();
    }

    public static string CreateAggregateTelemetryCollectorPartial(StringBuilder sb, in ImmutableArray<TelemetryMetricGenerator.EnumDetails> allMetrics)
        => CreateAggregateCollectorPartial(sb, CollectorType.Apm, in allMetrics);

    public static string CreateAggregateCiVisibilityTelemetryCollectorPartial(StringBuilder sb, in ImmutableArray<TelemetryMetricGenerator.EnumDetails> allMetrics)
        => CreateAggregateCollectorPartial(sb, CollectorType.Ci, in allMetrics);

    private static string CreateAggregateCollectorPartial(StringBuilder sb, CollectorType type, in ImmutableArray<TelemetryMetricGenerator.EnumDetails> allMetrics)
    {
        var sorted = allMetrics
           .Sort(
                (enum1, enum2) => (enum1, enum2) switch
                {
                    ({ MetricType: "count" }, { MetricType: not "count" }) => -1, // enum 1 is less than enum 2
                    ({ MetricType: not "count" }, { MetricType: "count" }) => 1, // enum 1 is less than enum 2
                    ({ MetricType: not "distribution" }, { MetricType: "distribution" }) => -1, // enum 1 is less than enum 2
                    ({ MetricType: "distribution" }, { MetricType: not "distribution" }) => 1, // enum 2 is after enum 1
                    _ when enum1.MetricType == enum2.MetricType => string.Compare(enum1.ShortName, enum2.ShortName, StringComparison.Ordinal),
                    _ => throw new InvalidOperationException($"Error comparing {enum1.ShortName} and {enum2.ShortName}"),
                });
        var distributions = sorted.RemoveAll(
            details => details.MetricType is not "distribution"
                    || !IsApplicable(type, details.IsApmMetric, details.IsCiAppMetric));

        var metrics = sorted.RemoveAll(
            details => details.MetricType is "distribution"
                    || !IsApplicable(type, details.IsApmMetric, details.IsCiAppMetric));

        sb.Clear();
        sb.Append(Constants.FileHeader);
        sb.Append($$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using Datadog.Trace.Telemetry.Metrics;
            using Datadog.Trace.Util;

            namespace Datadog.Trace.Telemetry;

            internal sealed partial class {{GetCollectorName(type)}}
            {
                private readonly Lazy<AggregatedMetrics> _aggregated = new();
                private MetricBuffer _buffer = new();
                private MetricBuffer _reserveBuffer = new();

                public void Record(PublicApiUsage publicApi)
                {
                    // This can technically overflow, but it's _very_ unlikely as we reset every 10s
                    // Negative values are normalized during polling
                    Interlocked.Increment(ref _buffer.PublicApiCounts[(int)publicApi]);
                }

                internal override void Clear()
                {
                    _reserveBuffer.Clear();
                    var buffer = Interlocked.Exchange(ref _buffer, _reserveBuffer);
                    buffer.Clear();
                }

                public MetricResults GetMetrics()
                {
                    List<MetricData>? metricData;
                    List<DistributionMetricData>? distributionData;

                    var aggregated = _aggregated.Value;
                    lock (aggregated)
                    {
                        metricData = GetMetricData(aggregated.PublicApiCounts
            """);
        foreach (var detail in metrics)
        {
            sb.Append(", aggregated.")
                .Append(detail.ShortName);
        }

        sb.AppendLine(");");

        var addedFirst = false;
        foreach (var detail in distributions)
        {
            if (!addedFirst)
            {
                sb.Append("            distributionData = GetDistributionData(");
                addedFirst = true;
            }
            else
            {
                sb.Append(", ");
            }

            sb.Append("aggregated.")
              .Append(detail.ShortName);
        }

        if (!addedFirst)
        {
            sb.Append("            distributionData = (null");
        }

        sb.AppendLine(");");
        sb.Append($$"""
                    }

                    return new(metricData, distributionData);
                }

                /// <summary>
                /// Internal for testing
                /// </summary>
                internal override void AggregateMetrics()
                {
                    var buffer = Interlocked.Exchange(ref _buffer, _reserveBuffer);

                    var aggregated = _aggregated.Value;
                    // _aggregated, containing the aggregated metrics, is not thread-safe
                    // and is also used when getting the metrics for serialization.
                    lock (aggregated)
                    {
                        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        AggregateMetric(buffer.PublicApiCounts, timestamp, aggregated.PublicApiCounts);

            """);

        foreach (var detail in metrics)
        {
            sb.Append("            AggregateMetric(buffer.")
              .Append(detail.ShortName)
              .Append(", timestamp, aggregated.")
              .Append(detail.ShortName)
              .AppendLine(");");
        }

        foreach (var detail in distributions)
        {
            sb.Append("            AggregateDistribution(buffer.")
              .Append(detail.ShortName)
              .Append(", aggregated.")
              .Append(detail.ShortName)
              .AppendLine(");");
        }

        sb.Append($$"""
                    }

                    // prepare the buffer for next time
                    buffer.Clear();
                    Interlocked.Exchange(ref _reserveBuffer, buffer);
                }

                /// <summary>
                /// Loop through the aggregated data, looking for any metrics that have points
                /// </summary>
                private List<MetricData>? GetMetricData(AggregatedMetric[] publicApis
            """);

        foreach (var detail in metrics)
        {
            sb.Append(", AggregatedMetric[] ")
              .Append(detail.ShortName.ToLowerInvariant());
        }

        sb.AppendLine("""
            )
                {
                    var apiLength = publicApis.Count(x => x.HasValues);
            """);

        foreach (var detail in metrics)
        {
            var varName = detail.ShortName.ToLowerInvariant();
            sb.Append("        var ")
              .Append(varName)
              .Append("Length = ")
              .Append(varName)
              .AppendLine(".Count(x => x.HasValues);");
        }

        sb.AppendLine()
          .Append("        var totalLength = apiLength");

        foreach (var detail in metrics)
        {
            sb.Append(" + ")
              .Append(detail.ShortName.ToLowerInvariant())
              .Append("Length");
        }

        sb.AppendLine(";")
          .Append("""
                    if (totalLength == 0)
                    {
                        return null;
                    }

                    var data = new List<MetricData>(totalLength);

                    if (apiLength > 0)
                    {
                        AddPublicApiMetricData(publicApis, data);
                    }

            """);

        foreach (var detail in metrics)
        {
            var varName = detail.ShortName.ToLowerInvariant();
            sb.AppendLine()
              .Append($$"""
                    if ({{varName}}Length > 0)
                    {
                        AddMetricData("{{detail.MetricType}}", {{varName}}, data, {{detail.ShortName}}EntryCounts, Get{{detail.ShortName}}Details);
                    }

            """);
        }

        sb.Append($$"""

                    return data;
                }

                private List<DistributionMetricData>? GetDistributionData(
            """);

        addedFirst = false;
        foreach (var detail in distributions)
        {
            if (addedFirst)
            {
                sb.Append(", AggregatedDistribution[] ");
            }
            else
            {
                sb.Append("AggregatedDistribution[] ");
                addedFirst = true;
            }

            sb.Append(detail.ShortName.ToLowerInvariant());
        }

        sb.AppendLine(")")
          .AppendLine("    {");

        foreach (var detail in distributions)
        {
            var varName = detail.ShortName.ToLowerInvariant();
            sb.Append("        var ")
              .Append(varName)
              .Append("Length = ")
              .Append(varName)
              .AppendLine(".Count(x => x.HasValues);");
        }

        sb.AppendLine()
          .Append("        var totalLength = 0");

        foreach (var detail in distributions)
        {
            sb.Append(" + ")
              .Append(detail.ShortName.ToLowerInvariant())
              .Append("Length");
        }

        sb.AppendLine(";")
          .Append("""
                          if (totalLength == 0)
                          {
                              return null;
                          }

                          var data = new List<DistributionMetricData>(totalLength);

                  """);

        foreach (var detail in distributions)
        {
            var varName = detail.ShortName.ToLowerInvariant();
            sb.AppendLine()
              .Append($$"""
                                if ({{varName}}Length > 0)
                                {
                                    AddDistributionData({{varName}}, data, {{detail.ShortName}}EntryCounts, Get{{detail.ShortName}}Details);
                                }

                        """);
        }

        sb.Append($$"""

                    return data;
                }

            """);

        foreach (var details in metrics)
        {
            sb.AppendLine($$"""

                  private static MetricDetails Get{{details.ShortName}}Details(int i)
                  {
                      var metric = ({{details.ShortName}})i;
                      return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
                  }
              """);
        }

        foreach (var details in distributions)
        {
            sb.AppendLine($$"""

                  private static MetricDetails Get{{details.ShortName}}Details(int i)
                  {
                      var metric = ({{details.ShortName}})i;
                      return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
                  }
              """);
        }

        sb.Append($$"""

                private class AggregatedMetrics
                {
                    public readonly AggregatedMetric[] PublicApiCounts;

            """);

        foreach (var detail in metrics)
        {
            sb.Append("        public readonly AggregatedMetric[] ")
              .Append(detail.ShortName)
              .AppendLine(";");
        }

        foreach (var detail in distributions)
        {
            sb.Append("        public readonly AggregatedDistribution[] ")
              .Append(detail.ShortName)
              .AppendLine(";");
        }

        sb.Append($$"""

                    public AggregatedMetrics()
                    {
                        PublicApiCounts = GetPublicApiCountBuffer();

            """);

        foreach (var detail in metrics)
        {
            sb.Append("            ")
              .Append(detail.ShortName)
              .Append(" = Get")
              .Append(detail.ShortName)
              .AppendLine("Buffer();");
        }

        foreach (var detail in distributions)
        {
            sb.Append("            ")
              .Append(detail.ShortName)
              .Append(" = Get")
              .Append(detail.ShortName)
              .AppendLine("Buffer();");
        }

        sb.Append($$"""
                    }
                }

                private sealed class MetricBuffer
                {
                    public readonly int[] PublicApiCounts;

            """);

        foreach (var detail in metrics)
        {
            sb.Append("        public readonly int[] ")
              .Append(detail.ShortName)
              .AppendLine(";");
        }

        foreach (var detail in distributions)
        {
            sb.Append("        public readonly BoundedConcurrentQueue<double>[] ")
              .Append(detail.ShortName)
              .AppendLine(";");
        }

        sb.Append($$"""

                    public MetricBuffer()
                    {
                        PublicApiCounts = new int[PublicApiUsageExtensions.Length];

            """);

        foreach (var detail in metrics)
        {
            sb.Append("            ")
              .Append(detail.ShortName)
              .Append(" = new int[")
              .Append(detail.ShortName)
              .AppendLine("Length];");
        }

        foreach (var detail in distributions)
        {
            sb.AppendLine($$"""
                        {{detail.ShortName}} = new BoundedConcurrentQueue<double>[{{detail.ShortName}}Length];

                        for (var i = {{detail.ShortName}}.Length - 1; i >= 0; i--)
                        {
                            {{detail.ShortName}}[i] = new BoundedConcurrentQueue<double>(queueLimit: 1000);
                        }

            """);
        }

        sb.Append($$"""
                    }

                    public void Clear()
                    {
                        for (var i = 0; i < PublicApiCounts.Length; i++)
                        {
                            PublicApiCounts[i] = 0;
                        }

            """);

        foreach (var detail in metrics)
        {
            sb.AppendLine($$"""

                        for (var i = 0; i < {{detail.ShortName}}.Length; i++)
                        {
                            {{detail.ShortName}}[i] = 0;
                        }
            """);
        }

        foreach (var detail in distributions)
        {
            sb.AppendLine($$"""

                        for (var i = 0; i < {{detail.ShortName}}.Length; i++)
                        {
                            while ({{detail.ShortName}}[i].TryDequeue(out _)) { }
                        }
            """);
        }

        sb.Append("""
                    }
                }
            }
            """);
        return sb.ToString();
    }

    private static string CreateCountCollectorPartial(StringBuilder sb, CollectorType type, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary, int[] metricsToLocation, int[] entryCounts)
    {
        var (collectorName, doRecord) = GetCollectorDetails(type, details.IsApmMetric, details.IsCiAppMetric);

        sb.Clear();
        sb.Append(Constants.FileHeader);
        sb.Append(
            $$"""
              using System.Threading;

              namespace Datadog.Trace.Telemetry;
              internal partial class {{collectorName}}
              {

              """);

        if (doRecord)
        {
            sb.Append(
                $$"""
                      private const int {{details.ShortName}}Length = {{entryCounts.Sum()}};

                      /// <summary>
                      /// Creates the buffer for the <see cref="{{details.FullyQualifiedName}}" /> values.
                      /// </summary>
                      private static AggregatedMetric[] Get{{details.ShortName}}Buffer()
                          => new AggregatedMetric[]
                          {

                  """);
            AddAggregatedMetrics(sb, in details, enumDictionary);
            sb.Append(
                $$"""
                          };
                  
                      /// <summary>
                      /// Gets an array of metric counts, indexed by integer value of the <see cref="{{details.FullyQualifiedName}}" />.
                      /// Each value represents the number of unique entries in the buffer returned by <see cref="Get{{details.ShortName}}Buffer()" />
                      /// It is equal to the cardinality of the tag combinations (or 1 if there are no tags)
                      /// </summary>
                      private static int[] {{details.ShortName}}EntryCounts { get; }
                          = new int[]{ 
                  """);

            foreach (var value in entryCounts)
            {
                sb.Append(value).Append(", ");
            }

            sb.AppendLine("};");
        }

        if (details.Names.AsArray() is { } names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                sb.AppendLine();
                var (property, metric) = names[i];
                var index = metricsToLocation[i];

                if (doRecord)
                {
                    WriteRecordCount(sb, in details, property, index, metric.TagFullyQualifiedNames, enumDictionary);
                }
                else
                {
                    WriteNoopCount(sb, in details, property, metric.TagFullyQualifiedNames, enumDictionary);
                }
            }
        }

        sb.Append("}");
        return sb.ToString();
    }

    private static string CreateGaugeCollectorPartial(StringBuilder sb, CollectorType type, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary, int[] metricsToLocation, int[] entryCounts)
    {
        var (collectorName, doRecord) = GetCollectorDetails(type, details.IsApmMetric, details.IsCiAppMetric);

        sb.Clear();
        sb.Append(Constants.FileHeader);
        sb.Append(
            $$"""
              using System.Threading;

              namespace Datadog.Trace.Telemetry;
              internal partial class {{collectorName}}
              {

              """);

        if (doRecord)
        {
            sb.Append(
                $$"""
                      private const int {{details.ShortName}}Length = {{entryCounts.Sum()}};

                      /// <summary>
                      /// Creates the buffer for the <see cref="{{details.FullyQualifiedName}}" /> values.
                      /// </summary>
                      private static AggregatedMetric[] Get{{details.ShortName}}Buffer()
                          => new AggregatedMetric[]
                          {

                  """);
            AddAggregatedMetrics(sb, in details, enumDictionary);
            sb.Append(
                $$"""
                          };
                  
                      /// <summary>
                      /// Gets an array of metric counts, indexed by integer value of the <see cref="{{details.FullyQualifiedName}}" />.
                      /// Each value represents the number of unique entries in the buffer returned by <see cref="Get{{details.ShortName}}Buffer()" />
                      /// It is equal to the cardinality of the tag combinations (or 1 if there are no tags)
                      /// </summary>
                      private static int[] {{details.ShortName}}EntryCounts { get; }
                          = new int[]{ 
                  """);

            foreach (var value in entryCounts)
            {
                sb.Append(value).Append(", ");
            }

            sb.AppendLine("};");
        }

        if (details.Names.AsArray() is { } names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                sb.AppendLine();
                var (property, metric) = names[i];
                var index = metricsToLocation[i];

                if (doRecord)
                {
                    WriteRecordGauge(sb, in details, property, index, metric.TagFullyQualifiedNames, enumDictionary);
                }
                else
                {
                    WriteNoopGauge(sb, in details, property, metric.TagFullyQualifiedNames, enumDictionary);
                }
            }
        }

        sb.Append("}");
        return sb.ToString();
    }

    private static string CreateDistributionCollectorPartial(StringBuilder sb, CollectorType type, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary, int[] metricsToLocation, int[] entryCounts)
    {
        var (collectorName, doRecord) = GetCollectorDetails(type, details.IsApmMetric, details.IsCiAppMetric);
        sb.Clear();
        sb.Append(Constants.FileHeader);
        sb.Append(
            $$"""
              using System.Threading;

              namespace Datadog.Trace.Telemetry;
              internal partial class {{collectorName}}
              {

              """);

        if (doRecord)
        {
            sb.Append(
                $$"""
                      private const int {{details.ShortName}}Length = {{entryCounts.Sum()}};
                  
                      /// <summary>
                      /// Creates the buffer for the <see cref="{{details.FullyQualifiedName}}" /> values.
                      /// </summary>
                      private static AggregatedDistribution[] Get{{details.ShortName}}Buffer()
                          => new AggregatedDistribution[]
                          {

                  """);
            AddAggregatedMetrics(sb, in details, enumDictionary);
            sb.Append(
                $$"""
                          };
                  
                      /// <summary>
                      /// Gets an array of metric counts, indexed by integer value of the <see cref="{{details.FullyQualifiedName}}" />.
                      /// Each value represents the number of unique entries in the buffer returned by <see cref="Get{{details.ShortName}}Buffer()" />
                      /// It is equal to the cardinality of the tag combinations (or 1 if there are no tags)
                      /// </summary>
                      private static int[] {{details.ShortName}}EntryCounts { get; }
                          = new int[]{ 
                  """);

            foreach (var value in entryCounts)
            {
                sb.Append(value).Append(", ");
            }

            sb.AppendLine("};");
        }

        if (details.Names.AsArray() is { } names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                sb.AppendLine();
                var (property, metric) = names[i];
                var index = metricsToLocation[i];

                if (doRecord)
                {
                    WriteRecordDistribution(sb, in details, property, index, metric.TagFullyQualifiedNames, enumDictionary);
                }
                else
                {
                    WriteNoopDistribution(sb, in details, property, metric.TagFullyQualifiedNames, enumDictionary);
                }
            }
        }

        sb.Append("}");
        return sb.ToString();
    }

    private static (string CollectorName, bool DoRecord) GetCollectorDetails(CollectorType type, bool isApmMetric, bool isCiAppMetric)
        => (GetCollectorName(type), IsApplicable(type, isApmMetric, isCiAppMetric));

    private static string GetCollectorName(CollectorType type)
        => type switch
        {
            CollectorType.Apm => "MetricsTelemetryCollector",
            CollectorType.Ci => "CiVisibilityMetricsTelemetryCollector",
            CollectorType.Null => "NullMetricsTelemetryCollector",
            _ => throw new InvalidOperationException("Unknown collector type " + type),
        };

    private static bool IsApplicable(CollectorType type, bool isApmMetric, bool isCiAppMetric)
        => (type, isApmMetric, isCiAppMetric) switch
        {
            (CollectorType.Ci, _, true) => true,
            (CollectorType.Apm, true, _) => true,
            _ => false,
        };

    private static void AddAggregatedMetrics(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, Dictionary<string, EquatableArray<string>> enumDictionary)
    {
        var names = details.Names.AsArray();
        if (names is null)
        {
            return;
        }

        var index = 0;
        foreach (var (_, metric) in names)
        {
            sb.AppendLine(
                $$"""
                            // {{metric.MetricName}}, index = {{index}}
                """);
            const string prefix =
                """
                            new(
                """;

            // Write the cartesian product of all the arrays
            var tags = metric.TagFullyQualifiedNames.AsArray() ?? [];
            List<string[]> tagValues = new(tags.Length);
            bool haveTags = false;

            foreach (var tag in tags)
            {
                var values = enumDictionary[tag].AsArray() ?? [];
                tagValues.Add([..values]);
                haveTags |= values.Length > 0;
            }

            if (haveTags)
            {
                // Could reduce allocations here, e.g. use stackalloc, but in a loop so meh
                var indices = new int[tagValues.Count];
                while (true)
                {
                    index++;
                    // Build the current combination
                    // if every entry is empty, we write null instead of an array
                    // so need to check first
                    bool haveNonNullValues = false;
                    for (var i = 0; i < tagValues.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(tagValues[i][indices[i]]))
                        {
                            haveNonNullValues = true;
                            break;
                        }
                    }

                    if (haveNonNullValues)
                    {
                        // write the full set
                        sb.Append($$"""{{prefix}}new[] { """);
                        for (var i = 0; i < tagValues.Count; i++)
                        {
                            WriteAllValues(sb, tagValues[i][indices[i]]);
                        }

                        sb.Remove(sb.Length - 2, 2); // remove the final ', '
                        sb.AppendLine(" }),");
                    }
                    else
                    {
                        // no non-empty tags
                        sb.AppendLine($$"""{{prefix}}null),""");
                    }

                    // Advance to the next combination
                    var incrementIndex = tagValues.Count - 1;
                    while (incrementIndex >= 0)
                    {
                        indices[incrementIndex]++;
                        if (indices[incrementIndex] < tagValues[incrementIndex].Length)
                        {
                            // We've successfully incremented this index, so we're ready to write the next combo
                            break;
                        }

                        // we went off the end, so reset, and increment the first tag
                        indices[incrementIndex] = 0;
                        incrementIndex--;
                    }

                    // If we've wrapped around at the first index, we're done
                    if (incrementIndex < 0)
                    {
                        break;
                    }
                }
            }
            else
            {
                // no tags
                index++;
                sb.AppendLine($$"""{{prefix}}null),""");
            }
        }

        static void WriteAllValues(StringBuilder sb, string tagValue)
        {
            // split the description on `;`, to allow writing _multiple_ tags with a single enum
            var previousSeparator = 0;
            var isFinished = false;
            while (!isFinished)
            {
                var nextSeparator = tagValue.IndexOf(';', previousSeparator);
                (isFinished, var length) = nextSeparator == -1
                                                       ? (true, tagValue.Length - previousSeparator)
                                                       : (false, nextSeparator - previousSeparator);

                if (length > 0)
                {
                    sb.Append('"')
                      .Append(tagValue, previousSeparator, length)
                      .Append("\", ");
                }

                previousSeparator = nextSeparator + 1;
            }
        }
    }

    private static string GetNames(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details)
        => GetValues(sb, in details, static (s, m) => s.Append('"').Append(m.MetricName).Append('"'));

    private static string GetIsCommon(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details)
        => GetValues(sb, in details, static (s, m) => s.Append("false"), m => !m.IsCommon);

    private static string GetNamespaces(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details)
        => GetValues(sb, in details, static (s, m) => s.Append('"').Append(m.NameSpace).Append('"'), static m => !string.IsNullOrEmpty(m.NameSpace));

    private static string GetValues(
        StringBuilder sb,
        in TelemetryMetricGenerator.EnumDetails details,
        Action<StringBuilder, TelemetryMetricGenerator.MetricDetails> action,
        Func<TelemetryMetricGenerator.MetricDetails, bool>? predicate = null)
    {
        var names = details.Names.AsArray();
        if (names is null)
        {
            return string.Empty;
        }

        sb.Clear();
        foreach (var (property, metric) in names)
        {
            if (predicate is { } && !predicate(metric))
            {
                continue;
            }

            sb.Append(
                   @"
            ")
              .Append(details.FullyQualifiedName)
              .Append('.')
              .Append(property)
              .Append(" => ");
            action(sb, metric);
            sb.Append(',');
        }

        return sb.ToString();
    }

    private static void WriteRecordCount(StringBuilder sb,  in TelemetryMetricGenerator.EnumDetails details, string property, int index, EquatableArray<string> tagNames, Dictionary<string, EquatableArray<string>> enumDictionary)
    {
        var tagArray = tagNames.AsArray() ?? [];
        if (tagArray.Length == 0)
        {
            // we don't need to keep this separate technically, could easily inline it, but it would change the generated code
            // very slightly (though the IL will remain the same)

            sb.AppendLine(
                $$"""
                      public void Record{{details.ShortName}}{{property}}(int increment = 1)
                      {
                          Interlocked.Add(ref _buffer.{{details.ShortName}}[{{index}}], increment);
                      }
                  """);
            return;
        }

        // Produces something similar to this:
        // public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, int increment = 1)
        // {
        //     var index = {{index}} + ((int)tag1 * {{tag2EntryCount}}) + (int)tag2;
        //     Interlocked.Add(ref _buffer.{{details.ShortName}}[index], increment);
        // }

        sb.Append(
            $$"""
                  public void Record{{details.ShortName}}{{property}}(
              """);

        WriteTagArgList(sb, tagArray);

        sb.Append(
            $$"""
              int increment = 1)
                  {
                      var index = {{index}}
              """);

        WriteTagIndices(sb, enumDictionary, tagArray);

        sb.AppendLine(
            $$"""
              ;
                      Interlocked.Add(ref _buffer.{{details.ShortName}}[index], increment);
                  }
              """);
    }

    private static void WriteRecordGauge(StringBuilder sb,  in TelemetryMetricGenerator.EnumDetails details, string property, int index, EquatableArray<string> tagNames, Dictionary<string, EquatableArray<string>> enumDictionary)
    {
        var tagArray = tagNames.AsArray() ?? [];
        if (tagArray.Length == 0)
        {
            // we don't need to keep this separate technically, could easily inline it, but it would change the generated code
            // very slightly (though the IL will remain the same)

            sb.AppendLine(
                $$"""
                      public void Record{{details.ShortName}}{{property}}(int value)
                      {
                          Interlocked.Exchange(ref _buffer.{{details.ShortName}}[{{index}}], value);
                      }
                  """);
            return;
        }

        // Produces something similar to this:
        // public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, int value)
        // {
        //     var index = {{index}} + ((int)tag1 * {{tag2EntryCount}}) + (int)tag2;
        //     Interlocked.Exchange(ref _buffer.{{details.ShortName}}[index], value);
        // }

        sb.Append(
            $$"""
                  public void Record{{details.ShortName}}{{property}}(
              """);

        WriteTagArgList(sb, tagArray);

        sb.Append(
            $$"""
              int value)
                  {
                      var index = {{index}}
              """);

        WriteTagIndices(sb, enumDictionary, tagArray);

        sb.AppendLine(
            $$"""
              ;
                      Interlocked.Exchange(ref _buffer.{{details.ShortName}}[index], value);
                  }
              """);
    }

    private static void WriteRecordDistribution(StringBuilder sb,  in TelemetryMetricGenerator.EnumDetails details, string property, int index, EquatableArray<string> tagNames, Dictionary<string, EquatableArray<string>> enumDictionary)
    {
        var tagArray = tagNames.AsArray() ?? [];
        if (tagArray.Length == 0)
        {
            // we don't need to keep this separate technically, could easily inline it, but it would change the generated code
            // very slightly (though the IL will remain the same)
            sb.AppendLine(
                $$"""
                      public void Record{{details.ShortName}}{{property}}(double value)
                      {
                          _buffer.{{details.ShortName}}[{{index}}].TryEnqueue(value);
                      }
                  """);
            return;
        }

        // Produces something similar to this:
        // public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, int value)
        // {
        //     var index = {{index}} + ((int)tag1 * {{tag2EntryCount}}) + (int)tag2;
        //     Interlocked.Exchange(ref _buffer.{{details.ShortName}}[index], value);
        // }

        sb.Append(
            $$"""
                  public void Record{{details.ShortName}}{{property}}(
              """);

        WriteTagArgList(sb, tagArray);

        sb.Append(
            $$"""
              double value)
                  {
                      var index = {{index}}
              """);

        WriteTagIndices(sb, enumDictionary, tagArray);

        sb.AppendLine(
            $$"""
              ;
                      _buffer.{{details.ShortName}}[index].TryEnqueue(value);
                  }
              """);
    }

    private static void WriteTagArgList(StringBuilder sb, string[] tagArray)
    {
        for (var i = 0; i < tagArray.Length; i++)
        {
            sb.Append(tagArray[i])
              .Append(" tag");

            if (tagArray.Length > 1)
            {
                sb.Append(i + 1);
            }

            sb.Append(", ");
        }
    }

    private static void WriteTagIndices(StringBuilder sb, Dictionary<string, EquatableArray<string>> enumDictionary, string[] tagArray)
    {
        if (tagArray.Length == 0)
        {
            return;
        }

        Span<int> multipliers = stackalloc int[tagArray.Length];
        var indexer = 1;

        // set the multipliers for each tag
        for (var i = tagArray.Length - 1; i >= 0; i--)
        {
            multipliers[i] = indexer;
            indexer *= enumDictionary.TryGetValue(tagArray[i], out var entries) ? entries.Count : 1;
        }

        for (var i = 0; i < tagArray.Length; i++)
        {
            var multiplier = multipliers[i];
            if (multiplier == 1)
            {
                sb.Append(" + (int)tag");

                if (tagArray.Length > 1)
                {
                    sb.Append(i + 1);
                }
            }
            else
            {
                sb.Append(" + ((int)tag")
                  .Append(i + 1)
                  .Append(" * ")
                  .Append(multiplier)
                  .Append(')');
            }
        }
    }

    private static void WriteNoopCount(StringBuilder sb,  in TelemetryMetricGenerator.EnumDetails details, string property, EquatableArray<string> tagNames, Dictionary<string, EquatableArray<string>> enumDictionary)
    {
        var tagArray = tagNames.AsArray() ?? [];
        if (tagArray.Length == 0)
        {
            // we don't need to keep this separate technically, could easily inline it, but it would change the generated code
            // very slightly (though the IL will remain the same)
            sb.AppendLine(
                $$"""
                      public void Record{{details.ShortName}}{{property}}(int increment = 1)
                      {
                      }
                  """);
            return;
        }

        // Produces something similar to this:
        // public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, int increment = 1)
        // {
        // }

        sb.Append(
            $$"""
                  public void Record{{details.ShortName}}{{property}}(
              """);

        WriteTagArgList(sb, tagArray);

        sb.AppendLine(
              """
              int increment = 1)
                  {
                  }
              """);
    }

    private static void WriteNoopGauge(StringBuilder sb,  in TelemetryMetricGenerator.EnumDetails details, string property, EquatableArray<string> tagNames, Dictionary<string, EquatableArray<string>> enumDictionary)
    {
        var tagArray = tagNames.AsArray() ?? [];
        if (tagArray.Length == 0)
        {
            // we don't need to keep this separate technically, could easily inline it, but it would change the generated code
            // very slightly (though the IL will remain the same)
            sb.AppendLine(
                $$"""
                      public void Record{{details.ShortName}}{{property}}(int value)
                      {
                      }
                  """);
            return;
        }

        // Produces something similar to this:
        // public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, int value)
        // {
        // }

        sb.Append(
            $$"""
                  public void Record{{details.ShortName}}{{property}}(
              """);

        WriteTagArgList(sb, tagArray);

        sb.AppendLine(
              """
              int value)
                  {
                  }
              """);
    }

    private static void WriteNoopDistribution(StringBuilder sb,  in TelemetryMetricGenerator.EnumDetails details, string property, EquatableArray<string> tagNames, Dictionary<string, EquatableArray<string>> enumDictionary)
    {
        var tagArray = tagNames.AsArray() ?? [];
        if (tagArray.Length == 0)
        {
            // we don't need to keep this separate technically, could easily inline it, but it would change the generated code
            // very slightly (though the IL will remain the same)
            sb.AppendLine(
                $$"""
                      public void Record{{details.ShortName}}{{property}}(double value)
                      {
                      }
                  """);
            return;
        }

        // Produces something similar to this:
        // public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, int value)
        // {
        // }

        sb.Append(
            $$"""
                  public void Record{{details.ShortName}}{{property}}(
              """);

        WriteTagArgList(sb, tagArray);

        sb.AppendLine(
              """
              double value)
                  {
                  }
              """);
    }
}
