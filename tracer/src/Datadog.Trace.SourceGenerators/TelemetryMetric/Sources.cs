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

                if (metric.Tag2FullyQualifiedName is { } tagName2)
                {
                    var tagName1 = metric.Tag1FullyQualifiedName!;
                    sb.AppendLine(
                        $$"""
                            public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, int increment = 1);
                        """);
                }
                else if (metric.Tag1FullyQualifiedName is { } tagName)
                {
                    sb.AppendLine(
                        $$"""
                            public void Record{{details.ShortName}}{{property}}({{tagName}} tag, int increment = 1);
                        """);
                }
                else
                {
                    sb.AppendLine(
                        $$"""
                            public void Record{{details.ShortName}}{{property}}(int increment = 1);
                        """);
                }
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

                if (metric.Tag2FullyQualifiedName is { } tagName2)
                {
                    var tagName1 = metric.Tag1FullyQualifiedName!;
                    sb.AppendLine(
                        $$"""
                            public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, int value);
                        """);
                }
                else if (metric.Tag1FullyQualifiedName is { } tagName)
                {
                    sb.AppendLine(
                        $$"""
                            public void Record{{details.ShortName}}{{property}}({{tagName}} tag, int value);
                        """);
                }
                else
                {
                    sb.AppendLine(
                        $$"""
                            public void Record{{details.ShortName}}{{property}}(int value);
                        """);
                }
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

                if (metric.Tag2FullyQualifiedName is { } tagName2)
                {
                    var tagName1 = metric.Tag1FullyQualifiedName!;
                    sb.AppendLine(
                        $$"""
                            public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, double value);
                        """);
                }
                else if (metric.Tag1FullyQualifiedName is { } tagName)
                {
                    sb.AppendLine(
                        $$"""
                            public void Record{{details.ShortName}}{{property}}({{tagName}} tag, double value);
                        """);
                }
                else
                {
                    sb.AppendLine(
                        $$"""
                            public void Record{{details.ShortName}}{{property}}(double value);
                        """);
                }
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

            internal partial class {{GetCollectorName(type)}}
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

                protected class MetricBuffer
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

                if (metric.Tag2FullyQualifiedName is { } tagName2)
                {
                    if (doRecord)
                    {
                        WriteRecordCount(sb, in details, property, index, metric.Tag1FullyQualifiedName!, tagName2, enumDictionary[tagName2].Count);
                    }
                    else
                    {
                        WriteNoopCount(sb, in details, property, metric.Tag1FullyQualifiedName!, tagName2);
                    }
                }
                else if (metric.Tag1FullyQualifiedName is { } tagName)
                {
                    if (doRecord)
                    {
                        WriteRecordCount(sb, in details, property, index, tagName);
                    }
                    else
                    {
                        WriteNoopCount(sb, in details, property, tagName);
                    }
                }
                else
                {
                    if (doRecord)
                    {
                        WriteRecordCount(sb, details, property, index);
                    }
                    else
                    {
                        WriteNoopCount(sb, in details, property);
                    }
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

                if (metric.Tag2FullyQualifiedName is { } tagName2)
                {
                    if (doRecord)
                    {
                        WriteRecordGauge(sb, in details, property, index, metric.Tag1FullyQualifiedName!, tagName2, enumDictionary[tagName2].Count);
                    }
                    else
                    {
                        WriteNoopGauge(sb, in details, property, metric.Tag1FullyQualifiedName!, tagName2);
                    }
                }
                else if (metric.Tag1FullyQualifiedName is { } tagName)
                {
                    if (doRecord)
                    {
                        WriteRecordGauge(sb, in details, property, index, tagName);
                    }
                    else
                    {
                        WriteNoopGauge(sb, in details, property, tagName);
                    }
                }
                else
                {
                    if (doRecord)
                    {
                        WriteRecordGauge(sb, details, property, index);
                    }
                    else
                    {
                        WriteNoopGauge(sb, in details, property);
                    }
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

                if (metric.Tag2FullyQualifiedName is { } tagName2)
                {
                    if (doRecord)
                    {
                        WriteRecordDistribution(sb, in details, property, index, metric.Tag1FullyQualifiedName!, tagName2, enumDictionary[tagName2].Count);
                    }
                    else
                    {
                        WriteNoopDistribution(sb, in details, property, metric.Tag1FullyQualifiedName!, tagName2);
                    }
                }
                else if (metric.Tag1FullyQualifiedName is { } tagName)
                {
                    if (doRecord)
                    {
                        WriteRecordDistribution(sb, in details, property, index, tagName);
                    }
                    else
                    {
                        WriteNoopDistribution(sb, in details, property, tagName);
                    }
                }
                else
                {
                    if (doRecord)
                    {
                        WriteRecordDistribution(sb, details, property, index);
                    }
                    else
                    {
                        WriteNoopDistribution(sb, in details, property);
                    }
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

        var i = 0;
        foreach (var (_, metric) in names)
        {
            sb.AppendLine(
                $$"""
                            // {{metric.MetricName}}, index = {{i}}
                """);
            const string prefix =
                """
                            new(
                """;
            if (metric.Tag1FullyQualifiedName is { } tag1Type && enumDictionary[tag1Type].AsArray() is { } tag1Values)
            {
                foreach (var tag1Value in tag1Values)
                {
                    if (metric.Tag2FullyQualifiedName is { } tag2Type && enumDictionary[tag2Type].AsArray() is { } tag2Values)
                    {
                        foreach (var tag2Value in tag2Values)
                        {
                            i++;
                            sb.Append(prefix);

                            if (string.IsNullOrEmpty(tag1Value) && string.IsNullOrEmpty(tag2Value))
                            {
                                sb.AppendLine("null),");
                                continue;
                            }

                            sb.Append("new[] { ");

                            WriteAllValues(sb, tag1Value);
                            WriteAllValues(sb, tag2Value);

                            sb.Remove(sb.Length - 2, 2); // remove the final ', '
                            sb.AppendLine(" }),");
                        }
                    }
                    else
                    {
                        i++;
                        sb.Append(prefix);

                        if (string.IsNullOrEmpty(tag1Value))
                        {
                            sb.AppendLine("null),");
                            continue;
                        }

                        sb.Append("new[] { ");

                        WriteAllValues(sb, tag1Value);

                        sb.Remove(sb.Length - 2, 2); // remove the final ', '
                        sb.AppendLine(" }),");
                    }
                }
            }
            else
            {
                i++;
                sb
                   .Append(prefix)
                   .AppendLine("null),");
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

    private static void WriteRecordCount(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property, int index)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}(int increment = 1)
                  {
                      Interlocked.Add(ref _buffer.{{details.ShortName}}[{{index}}], increment);
                  }
              """);
    }

    private static void WriteRecordCount(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property, int index, string tagName)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}({{tagName}} tag, int increment = 1)
                  {
                      var index = {{index}} + (int)tag;
                      Interlocked.Add(ref _buffer.{{details.ShortName}}[index], increment);
                  }
              """);
    }

    private static void WriteRecordCount(StringBuilder sb,  in TelemetryMetricGenerator.EnumDetails details, string property, int index, string tagName1, string tagName2, int tag2EntryCount)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, int increment = 1)
                  {
                      var index = {{index}} + ((int)tag1 * {{tag2EntryCount}}) + (int)tag2;
                      Interlocked.Add(ref _buffer.{{details.ShortName}}[index], increment);
                  }
              """);
    }

    private static void WriteRecordGauge(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property, int index)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}(int value)
                  {
                      Interlocked.Exchange(ref _buffer.{{details.ShortName}}[{{index}}], value);
                  }
              """);
    }

    private static void WriteRecordGauge(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property, int index, string tagName)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}({{tagName}} tag, int value)
                  {
                      var index = {{index}} + (int)tag;
                      Interlocked.Exchange(ref _buffer.{{details.ShortName}}[index], value);
                  }
              """);
    }

    private static void WriteRecordGauge(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property, int index, string tagName1, string tagName2, int tag2EntryCount)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, int value)
                  {
                      var index = {{index}} + ((int)tag1 * {{tag2EntryCount}}) + (int)tag2;
                      Interlocked.Exchange(ref _buffer.{{details.ShortName}}[index], value);
                  }
              """);
    }

    private static void WriteRecordDistribution(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property, int index)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}(double value)
                  {
                      _buffer.{{details.ShortName}}[{{index}}].TryEnqueue(value);
                  }
              """);
    }

    private static void WriteRecordDistribution(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property, int index, string tagName)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}({{tagName}} tag, double value)
                  {
                      var index = {{index}} + (int)tag;
                      _buffer.{{details.ShortName}}[index].TryEnqueue(value);
                  }
              """);
    }

    private static void WriteRecordDistribution(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property, int index, string tagName1, string tagName2, int tag2EntryCount)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, double value)
                  {
                      var index = {{index}} + ((int)tag1 * {{tag2EntryCount}}) + (int)tag2;
                      _buffer.{{details.ShortName}}[index].TryEnqueue(value);
                  }
              """);
    }

    private static void WriteNoopCount(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}(int increment = 1)
                  {
                  }
              """);
    }

    private static void WriteNoopCount(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property, string tagName)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}({{tagName}} tag, int increment = 1)
                  {
                  }
              """);
    }

    private static void WriteNoopCount(StringBuilder sb,  in TelemetryMetricGenerator.EnumDetails details, string property, string tagName1, string tagName2)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, int increment = 1)
                  {
                  }
              """);
    }

    private static void WriteNoopGauge(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}(int value)
                  {
                  }
              """);
    }

    private static void WriteNoopGauge(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property, string tagName)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}({{tagName}} tag, int value)
                  {
                  }
              """);
    }

    private static void WriteNoopGauge(StringBuilder sb,  in TelemetryMetricGenerator.EnumDetails details, string property, string tagName1, string tagName2)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, int value)
                  {
                  }
              """);
    }

    private static void WriteNoopDistribution(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}(double value)
                  {
                  }
              """);
    }

    private static void WriteNoopDistribution(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property, string tagName)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}({{tagName}} tag, double value)
                  {
                  }
              """);
    }

    private static void WriteNoopDistribution(StringBuilder sb, in TelemetryMetricGenerator.EnumDetails details, string property, string tagName1, string tagName2)
    {
        sb.AppendLine(
            $$"""
                  public void Record{{details.ShortName}}{{property}}({{tagName1}} tag1, {{tagName2}} tag2, double value)
                  {
                  }
              """);
    }
}
