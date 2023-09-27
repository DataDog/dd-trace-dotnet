// <copyright file="TelemetryMetricGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.TelemetryMetric;
using Datadog.Trace.SourceGenerators.TelemetryMetric.Diagnostics;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.SourceGenerators.Tests;

public class TelemetryMetricGeneratorTests
{
    [Fact] // edge case, not actually useful
    public void CanGenerateExtensionWithNoMembers()
    {
        const string input = """
            using Datadog.Trace.SourceGenerators;
            namespace MyTests.TestMetricNameSpace;

            [TelemetryMetricType("count")]
            public enum TestMetric
            { 
            }
            """;

        const string expectedEnum = Constants.FileHeader + """
            namespace MyTests.TestMetricNameSpace;
            internal static partial class TestMetricExtensions
            {
                /// <summary>
                /// The number of separate metrics in the <see cref="MyTests.TestMetricNameSpace.TestMetric" /> metric.
                /// </summary>
                public const int Length = 0;

                /// <summary>
                /// Gets the metric name for the provided metric
                /// </summary>
                /// <param name="metric">The metric to get the name for</param>
                /// <returns>The datadog metric name</returns>
                public static string GetName(this MyTests.TestMetricNameSpace.TestMetric metric)
                    => metric switch
                    {
                        _ => null!,
                    };

                /// <summary>
                /// Gets whether the metric is a "common" metric, used by all tracers
                /// </summary>
                /// <param name="metric">The metric to check</param>
                /// <returns>True if the metric is a "common" metric, used by all languages</returns>
                public static bool IsCommon(this MyTests.TestMetricNameSpace.TestMetric metric)
                    => metric switch
                    {
                        _ => true,
                    };

                /// <summary>
                /// Gets the custom namespace for the provided metric
                /// </summary>
                /// <param name="metric">The metric to get the name for</param>
                /// <returns>The datadog metric name</returns>
                public static string? GetNamespace(this MyTests.TestMetricNameSpace.TestMetric metric)
                    => metric switch
                    {
                        _ => null,
                    };
            }
            """;

        const string expectedCollector = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class MetricsTelemetryCollector
            {
                private const int TestMetricLength = 0;

                /// <summary>
                /// Creates the buffer for the <see cref="MyTests.TestMetricNameSpace.TestMetric" /> values.
                /// </summary>
                private static AggregatedMetric[] GetTestMetricBuffer()
                    => new AggregatedMetric[]
                    {
                    };

                /// <summary>
                /// Gets an array of metric counts, indexed by integer value of the <see cref="MyTests.TestMetricNameSpace.TestMetric" />.
                /// Each value represents the number of unique entries in the buffer returned by <see cref="GetTestMetricBuffer()" />
                /// It is equal to the cardinality of the tag combinations (or 1 if there are no tags)
                /// </summary>
                private static int[] TestMetricEntryCounts { get; }
                    = new int[]{ };
            }
            """;

        const string expectedCiVisibility = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class CiVisibilityMetricsTelemetryCollector
            {
            }
            """;

        const string expectedInterface = Constants.FileHeader + """
            namespace Datadog.Trace.Telemetry;
            internal partial interface IMetricsTelemetryCollector
            {}
            """;

        const string expectedNull = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class NullMetricsTelemetryCollector
            {
            }
            """;

        const string expectedAggregateCollector = Constants.FileHeader + $$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using Datadog.Trace.Telemetry.Metrics;
            using Datadog.Trace.Util;

            namespace Datadog.Trace.Telemetry;

            internal partial class MetricsTelemetryCollector
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
                        metricData = GetMetricData(aggregated.PublicApiCounts, aggregated.TestMetric);
                        distributionData = (null);
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
                        AggregateMetric(buffer.TestMetric, timestamp, aggregated.TestMetric);
                    }

                    // prepare the buffer for next time
                    buffer.Clear();
                    Interlocked.Exchange(ref _reserveBuffer, buffer);
                }

                /// <summary>
                /// Loop through the aggregated data, looking for any metrics that have points
                /// </summary>
                private List<MetricData>? GetMetricData(AggregatedMetric[] publicApis, AggregatedMetric[] testmetric)
                {
                    var apiLength = publicApis.Count(x => x.HasValues);
                    var testmetricLength = testmetric.Count(x => x.HasValues);

                    var totalLength = apiLength + testmetricLength;
                    if (totalLength == 0)
                    {
                        return null;
                    }

                    var data = new List<MetricData>(totalLength);

                    if (apiLength > 0)
                    {
                        AddPublicApiMetricData(publicApis, data);
                    }

                    if (testmetricLength > 0)
                    {
                        AddMetricData("count", testmetric, data, TestMetricEntryCounts, GetTestMetricDetails);
                    }

                    return data;
                }

                private List<DistributionMetricData>? GetDistributionData()
                {

                    var totalLength = 0;
                    if (totalLength == 0)
                    {
                        return null;
                    }

                    var data = new List<DistributionMetricData>(totalLength);

                    return data;
                }

                private static MetricDetails GetTestMetricDetails(int i)
                {
                    var metric = (TestMetric)i;
                    return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
                }

                private class AggregatedMetrics
                {
                    public readonly AggregatedMetric[] PublicApiCounts;
                    public readonly AggregatedMetric[] TestMetric;

                    public AggregatedMetrics()
                    {
                        PublicApiCounts = GetPublicApiCountBuffer();
                        TestMetric = GetTestMetricBuffer();
                    }
                }

                protected class MetricBuffer
                {
                    public readonly int[] PublicApiCounts;
                    public readonly int[] TestMetric;

                    public MetricBuffer()
                    {
                        PublicApiCounts = new int[PublicApiUsageExtensions.Length];
                        TestMetric = new int[TestMetricLength];
                    }

                    public void Clear()
                    {
                        for (var i = 0; i < PublicApiCounts.Length; i++)
                        {
                            PublicApiCounts[i] = 0;
                        }

                        for (var i = 0; i < TestMetric.Length; i++)
                        {
                            TestMetric[i] = 0;
                        }
                    }
                }
            }
            """;

        const string expectedCiVisibilityAggregateCollector = Constants.FileHeader + $$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using Datadog.Trace.Telemetry.Metrics;
            using Datadog.Trace.Util;

            namespace Datadog.Trace.Telemetry;

            internal partial class CiVisibilityMetricsTelemetryCollector
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
                        metricData = GetMetricData(aggregated.PublicApiCounts);
                        distributionData = (null);
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
                    }

                    // prepare the buffer for next time
                    buffer.Clear();
                    Interlocked.Exchange(ref _reserveBuffer, buffer);
                }

                /// <summary>
                /// Loop through the aggregated data, looking for any metrics that have points
                /// </summary>
                private List<MetricData>? GetMetricData(AggregatedMetric[] publicApis)
                {
                    var apiLength = publicApis.Count(x => x.HasValues);

                    var totalLength = apiLength;
                    if (totalLength == 0)
                    {
                        return null;
                    }

                    var data = new List<MetricData>(totalLength);

                    if (apiLength > 0)
                    {
                        AddPublicApiMetricData(publicApis, data);
                    }

                    return data;
                }

                private List<DistributionMetricData>? GetDistributionData()
                {

                    var totalLength = 0;
                    if (totalLength == 0)
                    {
                        return null;
                    }

                    var data = new List<DistributionMetricData>(totalLength);

                    return data;
                }

                private class AggregatedMetrics
                {
                    public readonly AggregatedMetric[] PublicApiCounts;

                    public AggregatedMetrics()
                    {
                        PublicApiCounts = GetPublicApiCountBuffer();
                    }
                }

                protected class MetricBuffer
                {
                    public readonly int[] PublicApiCounts;

                    public MetricBuffer()
                    {
                        PublicApiCounts = new int[PublicApiUsageExtensions.Length];
                    }

                    public void Clear()
                    {
                        for (var i = 0; i < PublicApiCounts.Length; i++)
                        {
                            PublicApiCounts[i] = 0;
                        }
                    }
                }
            }
            """;

        var (diagnostics, trees) = TestHelpers.GetGeneratedTrees<TelemetryMetricGenerator>(input);
        using var scope = new AssertionScope();
        diagnostics.Should().BeEmpty();
        // tree 0 is the attributes
        trees.Length.Should().Be(8);
        trees[1].Should().Be(expectedEnum);
        trees[2].Should().Be(expectedCollector);
        trees[3].Should().Be(expectedCiVisibility);
        trees[4].Should().Be(expectedInterface);
        trees[5].Should().Be(expectedNull);
        trees[6].Should().Be(expectedAggregateCollector);
        trees[7].Should().Be(expectedCiVisibilityAggregateCollector);
    }

    [Fact]
    public void CanGenerateForCountMetrics()
    {
        var input = StandardGeneratedSource.GetStandardEnums("count");

        var expectedTestMetricCollector = Constants.FileHeader + $$"""
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class MetricsTelemetryCollector
            {
            {{StandardGeneratedSource.GetTestMetricBuffers()}}

                public void RecordTestMetricZeroTagMetric(int increment = 1)
                {
                    Interlocked.Add(ref _buffer.TestMetric[0], increment);
                }

                public void RecordTestMetricOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int increment = 1)
                {
                    var index = 1 + (int)tag;
                    Interlocked.Add(ref _buffer.TestMetric[index], increment);
                }

                public void RecordTestMetricTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int increment = 1)
                {
                    var index = 5 + ((int)tag1 * 3) + (int)tag2;
                    Interlocked.Add(ref _buffer.TestMetric[index], increment);
                }

                public void RecordTestMetricZeroAgainTagMetric(int increment = 1)
                {
                    Interlocked.Add(ref _buffer.TestMetric[17], increment);
                }
            }
            """;

        const string expectedTestMetricCiVisibility = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class CiVisibilityMetricsTelemetryCollector
            {

                public void RecordTestMetricZeroTagMetric(int increment = 1)
                {
                }

                public void RecordTestMetricOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int increment = 1)
                {
                }

                public void RecordTestMetricTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int increment = 1)
                {
                }

                public void RecordTestMetricZeroAgainTagMetric(int increment = 1)
                {
                }
            }
            """;

        const string expectedTestMetricInterface = Constants.FileHeader + """
            namespace Datadog.Trace.Telemetry;
            internal partial interface IMetricsTelemetryCollector
            {
                public void RecordTestMetricZeroTagMetric(int increment = 1);

                public void RecordTestMetricOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int increment = 1);

                public void RecordTestMetricTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int increment = 1);

                public void RecordTestMetricZeroAgainTagMetric(int increment = 1);
            }
            """;

        const string expectedTestMetricNull = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class NullMetricsTelemetryCollector
            {

                public void RecordTestMetricZeroTagMetric(int increment = 1)
                {
                }

                public void RecordTestMetricOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int increment = 1)
                {
                }

                public void RecordTestMetricTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int increment = 1)
                {
                }

                public void RecordTestMetricZeroAgainTagMetric(int increment = 1)
                {
                }
            }
            """;

        const string expectedTestCiMetricCollector = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class MetricsTelemetryCollector
            {

                public void RecordTestCiMetricCiZeroTagMetric(int increment = 1)
                {
                }

                public void RecordTestCiMetricCiOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int increment = 1)
                {
                }

                public void RecordTestCiMetricCiTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int increment = 1)
                {
                }
            }
            """;

        var expectedTestCiMetricCiVisibility = Constants.FileHeader + $$"""
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class CiVisibilityMetricsTelemetryCollector
            {
            {{StandardGeneratedSource.GetTestCiMetricBuffers()}}

                public void RecordTestCiMetricCiZeroTagMetric(int increment = 1)
                {
                    Interlocked.Add(ref _buffer.TestCiMetric[0], increment);
                }

                public void RecordTestCiMetricCiOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int increment = 1)
                {
                    var index = 1 + (int)tag;
                    Interlocked.Add(ref _buffer.TestCiMetric[index], increment);
                }

                public void RecordTestCiMetricCiTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int increment = 1)
                {
                    var index = 5 + ((int)tag1 * 3) + (int)tag2;
                    Interlocked.Add(ref _buffer.TestCiMetric[index], increment);
                }
            }
            """;

        const string expectedTestCiMetricInterface = Constants.FileHeader + """
            namespace Datadog.Trace.Telemetry;
            internal partial interface IMetricsTelemetryCollector
            {
                public void RecordTestCiMetricCiZeroTagMetric(int increment = 1);

                public void RecordTestCiMetricCiOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int increment = 1);

                public void RecordTestCiMetricCiTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int increment = 1);
            }
            """;

        const string expectedTestCiMetricNull = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class NullMetricsTelemetryCollector
            {

                public void RecordTestCiMetricCiZeroTagMetric(int increment = 1)
                {
                }

                public void RecordTestCiMetricCiOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int increment = 1)
                {
                }

                public void RecordTestCiMetricCiTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int increment = 1)
                {
                }
            }
            """;

        var expectedTestSharedMetricCollector = Constants.FileHeader + $$"""
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class MetricsTelemetryCollector
            {
            {{StandardGeneratedSource.GetTestSharedMetricBuffers()}}

                public void RecordTestSharedMetricSharedZeroTagMetric(int increment = 1)
                {
                    Interlocked.Add(ref _buffer.TestSharedMetric[0], increment);
                }

                public void RecordTestSharedMetricSharedOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int increment = 1)
                {
                    var index = 1 + (int)tag;
                    Interlocked.Add(ref _buffer.TestSharedMetric[index], increment);
                }

                public void RecordTestSharedMetricSharedTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int increment = 1)
                {
                    var index = 5 + ((int)tag1 * 3) + (int)tag2;
                    Interlocked.Add(ref _buffer.TestSharedMetric[index], increment);
                }
            }
            """;

        var expectedTestSharedMetricCiVisibility = Constants.FileHeader + $$"""
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class CiVisibilityMetricsTelemetryCollector
            {
            {{StandardGeneratedSource.GetTestSharedMetricBuffers()}}

                public void RecordTestSharedMetricSharedZeroTagMetric(int increment = 1)
                {
                    Interlocked.Add(ref _buffer.TestSharedMetric[0], increment);
                }

                public void RecordTestSharedMetricSharedOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int increment = 1)
                {
                    var index = 1 + (int)tag;
                    Interlocked.Add(ref _buffer.TestSharedMetric[index], increment);
                }

                public void RecordTestSharedMetricSharedTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int increment = 1)
                {
                    var index = 5 + ((int)tag1 * 3) + (int)tag2;
                    Interlocked.Add(ref _buffer.TestSharedMetric[index], increment);
                }
            }
            """;

        const string expectedTestSharedMetricInterface = Constants.FileHeader + """
            namespace Datadog.Trace.Telemetry;
            internal partial interface IMetricsTelemetryCollector
            {
                public void RecordTestSharedMetricSharedZeroTagMetric(int increment = 1);

                public void RecordTestSharedMetricSharedOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int increment = 1);

                public void RecordTestSharedMetricSharedTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int increment = 1);
            }
            """;

        const string expectedTestSharedMetricNull = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class NullMetricsTelemetryCollector
            {

                public void RecordTestSharedMetricSharedZeroTagMetric(int increment = 1)
                {
                }

                public void RecordTestSharedMetricSharedOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int increment = 1)
                {
                }

                public void RecordTestSharedMetricSharedTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int increment = 1)
                {
                }
            }
            """;

        var (diagnostics, trees) = TestHelpers.GetGeneratedTrees<TelemetryMetricGenerator>(input);
        using var scope = new AssertionScope();
        diagnostics.Should().BeEmpty();
        trees.Length.Should().Be(18);
        // tree 0 is the attributes
        trees[1].Should().Be(StandardGeneratedSource.TestMetricExtensions);
        trees[2].Should().Be(expectedTestMetricCollector);
        trees[3].Should().Be(expectedTestMetricCiVisibility);
        trees[4].Should().Be(expectedTestMetricInterface);
        trees[5].Should().Be(expectedTestMetricNull);
        trees[6].Should().Be(StandardGeneratedSource.TestCiMetricExtensions);
        trees[7].Should().Be(expectedTestCiMetricCollector);
        trees[8].Should().Be(expectedTestCiMetricCiVisibility);
        trees[9].Should().Be(expectedTestCiMetricInterface);
        trees[10].Should().Be(expectedTestCiMetricNull);
        trees[11].Should().Be(StandardGeneratedSource.TestSharedMetricExtensions);
        trees[12].Should().Be(expectedTestSharedMetricCollector);
        trees[13].Should().Be(expectedTestSharedMetricCiVisibility);
        trees[14].Should().Be(expectedTestSharedMetricInterface);
        trees[15].Should().Be(expectedTestSharedMetricNull);
        trees[16].Should().Be(StandardGeneratedSource.GetMetricAggregateCollector("count"));
        trees[17].Should().Be(StandardGeneratedSource.GetMetricCiVisibilityAggregateCollector("count"));
    }

    [Fact]
    public void CanGenerateForGaugeMetrics()
    {
        var input = StandardGeneratedSource.GetStandardEnums("gauge");

        var expectedTestMetricCollector = Constants.FileHeader + $$"""
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class MetricsTelemetryCollector
            {
            {{StandardGeneratedSource.GetTestMetricBuffers()}}

                public void RecordTestMetricZeroTagMetric(int value)
                {
                    Interlocked.Exchange(ref _buffer.TestMetric[0], value);
                }

                public void RecordTestMetricOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int value)
                {
                    var index = 1 + (int)tag;
                    Interlocked.Exchange(ref _buffer.TestMetric[index], value);
                }

                public void RecordTestMetricTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int value)
                {
                    var index = 5 + ((int)tag1 * 3) + (int)tag2;
                    Interlocked.Exchange(ref _buffer.TestMetric[index], value);
                }

                public void RecordTestMetricZeroAgainTagMetric(int value)
                {
                    Interlocked.Exchange(ref _buffer.TestMetric[17], value);
                }
            }
            """;

        const string expectedTestMetricCiVisibility = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class CiVisibilityMetricsTelemetryCollector
            {

                public void RecordTestMetricZeroTagMetric(int value)
                {
                }

                public void RecordTestMetricOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int value)
                {
                }

                public void RecordTestMetricTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int value)
                {
                }

                public void RecordTestMetricZeroAgainTagMetric(int value)
                {
                }
            }
            """;

        const string expectedTestMetricInterface = Constants.FileHeader + """
            namespace Datadog.Trace.Telemetry;
            internal partial interface IMetricsTelemetryCollector
            {
                public void RecordTestMetricZeroTagMetric(int value);

                public void RecordTestMetricOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int value);

                public void RecordTestMetricTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int value);

                public void RecordTestMetricZeroAgainTagMetric(int value);
            }
            """;

        const string expectedTestMetricNull = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class NullMetricsTelemetryCollector
            {

                public void RecordTestMetricZeroTagMetric(int value)
                {
                }

                public void RecordTestMetricOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int value)
                {
                }

                public void RecordTestMetricTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int value)
                {
                }

                public void RecordTestMetricZeroAgainTagMetric(int value)
                {
                }
            }
            """;

        // CI Visibility metric
        const string expectedTestCiMetricCollector = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class MetricsTelemetryCollector
            {

                public void RecordTestCiMetricCiZeroTagMetric(int value)
                {
                }

                public void RecordTestCiMetricCiOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int value)
                {
                }

                public void RecordTestCiMetricCiTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int value)
                {
                }
            }
            """;

        var expectedTestCiMetricCiVisibility = Constants.FileHeader + $$"""
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class CiVisibilityMetricsTelemetryCollector
            {
            {{StandardGeneratedSource.GetTestCiMetricBuffers()}}

                public void RecordTestCiMetricCiZeroTagMetric(int value)
                {
                    Interlocked.Exchange(ref _buffer.TestCiMetric[0], value);
                }

                public void RecordTestCiMetricCiOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int value)
                {
                    var index = 1 + (int)tag;
                    Interlocked.Exchange(ref _buffer.TestCiMetric[index], value);
                }

                public void RecordTestCiMetricCiTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int value)
                {
                    var index = 5 + ((int)tag1 * 3) + (int)tag2;
                    Interlocked.Exchange(ref _buffer.TestCiMetric[index], value);
                }
            }
            """;

        const string expectedTestCiMetricInterface = Constants.FileHeader + """
            namespace Datadog.Trace.Telemetry;
            internal partial interface IMetricsTelemetryCollector
            {
                public void RecordTestCiMetricCiZeroTagMetric(int value);

                public void RecordTestCiMetricCiOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int value);

                public void RecordTestCiMetricCiTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int value);
            }
            """;

        const string expectedTestCiMetricNull = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class NullMetricsTelemetryCollector
            {

                public void RecordTestCiMetricCiZeroTagMetric(int value)
                {
                }

                public void RecordTestCiMetricCiOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int value)
                {
                }

                public void RecordTestCiMetricCiTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int value)
                {
                }
            }
            """;

        // Shared metric
        var expectedTestSharedMetricCollector = Constants.FileHeader + $$"""
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class MetricsTelemetryCollector
            {
            {{StandardGeneratedSource.GetTestSharedMetricBuffers()}}

                public void RecordTestSharedMetricSharedZeroTagMetric(int value)
                {
                    Interlocked.Exchange(ref _buffer.TestSharedMetric[0], value);
                }

                public void RecordTestSharedMetricSharedOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int value)
                {
                    var index = 1 + (int)tag;
                    Interlocked.Exchange(ref _buffer.TestSharedMetric[index], value);
                }

                public void RecordTestSharedMetricSharedTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int value)
                {
                    var index = 5 + ((int)tag1 * 3) + (int)tag2;
                    Interlocked.Exchange(ref _buffer.TestSharedMetric[index], value);
                }
            }
            """;

        var expectedTestSharedMetricCiVisibility = Constants.FileHeader + $$"""
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class CiVisibilityMetricsTelemetryCollector
            {
            {{StandardGeneratedSource.GetTestSharedMetricBuffers()}}

                public void RecordTestSharedMetricSharedZeroTagMetric(int value)
                {
                    Interlocked.Exchange(ref _buffer.TestSharedMetric[0], value);
                }

                public void RecordTestSharedMetricSharedOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int value)
                {
                    var index = 1 + (int)tag;
                    Interlocked.Exchange(ref _buffer.TestSharedMetric[index], value);
                }

                public void RecordTestSharedMetricSharedTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int value)
                {
                    var index = 5 + ((int)tag1 * 3) + (int)tag2;
                    Interlocked.Exchange(ref _buffer.TestSharedMetric[index], value);
                }
            }
            """;

        const string expectedTestSharedMetricInterface = Constants.FileHeader + """
            namespace Datadog.Trace.Telemetry;
            internal partial interface IMetricsTelemetryCollector
            {
                public void RecordTestSharedMetricSharedZeroTagMetric(int value);

                public void RecordTestSharedMetricSharedOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int value);

                public void RecordTestSharedMetricSharedTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int value);
            }
            """;

        const string expectedTestSharedMetricNull = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class NullMetricsTelemetryCollector
            {

                public void RecordTestSharedMetricSharedZeroTagMetric(int value)
                {
                }

                public void RecordTestSharedMetricSharedOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, int value)
                {
                }

                public void RecordTestSharedMetricSharedTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, int value)
                {
                }
            }
            """;

        var (diagnostics, trees) = TestHelpers.GetGeneratedTrees<TelemetryMetricGenerator>(input);
        using var scope = new AssertionScope();
        diagnostics.Should().BeEmpty();
        trees.Length.Should().Be(18);
        // tree 0 is the attributes
        trees[1].Should().Be(StandardGeneratedSource.TestMetricExtensions);
        trees[2].Should().Be(expectedTestMetricCollector);
        trees[3].Should().Be(expectedTestMetricCiVisibility);
        trees[4].Should().Be(expectedTestMetricInterface);
        trees[5].Should().Be(expectedTestMetricNull);
        trees[6].Should().Be(StandardGeneratedSource.TestCiMetricExtensions);
        trees[7].Should().Be(expectedTestCiMetricCollector);
        trees[8].Should().Be(expectedTestCiMetricCiVisibility);
        trees[9].Should().Be(expectedTestCiMetricInterface);
        trees[10].Should().Be(expectedTestCiMetricNull);
        trees[11].Should().Be(StandardGeneratedSource.TestSharedMetricExtensions);
        trees[12].Should().Be(expectedTestSharedMetricCollector);
        trees[13].Should().Be(expectedTestSharedMetricCiVisibility);
        trees[14].Should().Be(expectedTestSharedMetricInterface);
        trees[15].Should().Be(expectedTestSharedMetricNull);
        trees[16].Should().Be(StandardGeneratedSource.GetMetricAggregateCollector("gauge"));
        trees[17].Should().Be(StandardGeneratedSource.GetMetricCiVisibilityAggregateCollector("gauge"));
    }

    [Fact]
    public void CanGenerateForDistributionMetrics()
    {
        var input = StandardGeneratedSource.GetStandardEnums("distribution");

        var expectedTestMetricCollector = Constants.FileHeader + $$"""
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class MetricsTelemetryCollector
            {
            {{StandardGeneratedSource.GetTestMetricBuffers(isDistribution: true)}}

                public void RecordTestMetricZeroTagMetric(double value)
                {
                    _buffer.TestMetric[0].TryEnqueue(value);
                }

                public void RecordTestMetricOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, double value)
                {
                    var index = 1 + (int)tag;
                    _buffer.TestMetric[index].TryEnqueue(value);
                }

                public void RecordTestMetricTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, double value)
                {
                    var index = 5 + ((int)tag1 * 3) + (int)tag2;
                    _buffer.TestMetric[index].TryEnqueue(value);
                }

                public void RecordTestMetricZeroAgainTagMetric(double value)
                {
                    _buffer.TestMetric[17].TryEnqueue(value);
                }
            }
            """;

        const string expectedTestMetricCiVisibility = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class CiVisibilityMetricsTelemetryCollector
            {

                public void RecordTestMetricZeroTagMetric(double value)
                {
                }

                public void RecordTestMetricOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, double value)
                {
                }

                public void RecordTestMetricTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, double value)
                {
                }

                public void RecordTestMetricZeroAgainTagMetric(double value)
                {
                }
            }
            """;

        const string expectedTestMetricInterface = Constants.FileHeader + """
            namespace Datadog.Trace.Telemetry;
            internal partial interface IMetricsTelemetryCollector
            {
                public void RecordTestMetricZeroTagMetric(double value);

                public void RecordTestMetricOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, double value);

                public void RecordTestMetricTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, double value);

                public void RecordTestMetricZeroAgainTagMetric(double value);
            }
            """;

        const string expectedTestMetricNull = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class NullMetricsTelemetryCollector
            {

                public void RecordTestMetricZeroTagMetric(double value)
                {
                }

                public void RecordTestMetricOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, double value)
                {
                }

                public void RecordTestMetricTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, double value)
                {
                }

                public void RecordTestMetricZeroAgainTagMetric(double value)
                {
                }
            }
            """;

        // Ci visibility metric
        const string expectedTestCiMetricCollector = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class MetricsTelemetryCollector
            {

                public void RecordTestCiMetricCiZeroTagMetric(double value)
                {
                }

                public void RecordTestCiMetricCiOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, double value)
                {
                }

                public void RecordTestCiMetricCiTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, double value)
                {
                }
            }
            """;

        var expectedTestCiMetricCiVisibility = Constants.FileHeader + $$"""
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class CiVisibilityMetricsTelemetryCollector
            {
            {{StandardGeneratedSource.GetTestCiMetricBuffers(isDistribution: true)}}

                public void RecordTestCiMetricCiZeroTagMetric(double value)
                {
                    _buffer.TestCiMetric[0].TryEnqueue(value);
                }

                public void RecordTestCiMetricCiOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, double value)
                {
                    var index = 1 + (int)tag;
                    _buffer.TestCiMetric[index].TryEnqueue(value);
                }

                public void RecordTestCiMetricCiTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, double value)
                {
                    var index = 5 + ((int)tag1 * 3) + (int)tag2;
                    _buffer.TestCiMetric[index].TryEnqueue(value);
                }
            }
            """;

        const string expectedTestCiMetricInterface = Constants.FileHeader + """
            namespace Datadog.Trace.Telemetry;
            internal partial interface IMetricsTelemetryCollector
            {
                public void RecordTestCiMetricCiZeroTagMetric(double value);

                public void RecordTestCiMetricCiOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, double value);

                public void RecordTestCiMetricCiTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, double value);
            }
            """;

        const string expectedTestCiMetricNull = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class NullMetricsTelemetryCollector
            {

                public void RecordTestCiMetricCiZeroTagMetric(double value)
                {
                }

                public void RecordTestCiMetricCiOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, double value)
                {
                }

                public void RecordTestCiMetricCiTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, double value)
                {
                }
            }
            """;

        // shared metric
        var expectedTestSharedMetricCollector = Constants.FileHeader + $$"""
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class MetricsTelemetryCollector
            {
            {{StandardGeneratedSource.GetTestSharedMetricBuffers(isDistribution: true)}}
            
                public void RecordTestSharedMetricSharedZeroTagMetric(double value)
                {
                    _buffer.TestSharedMetric[0].TryEnqueue(value);
                }

                public void RecordTestSharedMetricSharedOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, double value)
                {
                    var index = 1 + (int)tag;
                    _buffer.TestSharedMetric[index].TryEnqueue(value);
                }

                public void RecordTestSharedMetricSharedTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, double value)
                {
                    var index = 5 + ((int)tag1 * 3) + (int)tag2;
                    _buffer.TestSharedMetric[index].TryEnqueue(value);
                }
            }
            """;

        var expectedTestSharedMetricCiVisibility = Constants.FileHeader + $$"""
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class CiVisibilityMetricsTelemetryCollector
            {
            {{StandardGeneratedSource.GetTestSharedMetricBuffers(isDistribution: true)}}

                public void RecordTestSharedMetricSharedZeroTagMetric(double value)
                {
                    _buffer.TestSharedMetric[0].TryEnqueue(value);
                }

                public void RecordTestSharedMetricSharedOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, double value)
                {
                    var index = 1 + (int)tag;
                    _buffer.TestSharedMetric[index].TryEnqueue(value);
                }

                public void RecordTestSharedMetricSharedTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, double value)
                {
                    var index = 5 + ((int)tag1 * 3) + (int)tag2;
                    _buffer.TestSharedMetric[index].TryEnqueue(value);
                }
            }
            """;

        const string expectedTestSharedMetricInterface = Constants.FileHeader + """
            namespace Datadog.Trace.Telemetry;
            internal partial interface IMetricsTelemetryCollector
            {
                public void RecordTestSharedMetricSharedZeroTagMetric(double value);

                public void RecordTestSharedMetricSharedOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, double value);

                public void RecordTestSharedMetricSharedTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, double value);
            }
            """;

        const string expectedTestSharedMetricNull = Constants.FileHeader + """
            using System.Threading;

            namespace Datadog.Trace.Telemetry;
            internal partial class NullMetricsTelemetryCollector
            {

                public void RecordTestSharedMetricSharedZeroTagMetric(double value)
                {
                }

                public void RecordTestSharedMetricSharedOneTagMetric(MyTests.TestMetricNameSpace.LogLevel tag, double value)
                {
                }

                public void RecordTestSharedMetricSharedTwoTagMetric(MyTests.TestMetricNameSpace.LogLevel tag1, MyTests.TestMetricNameSpace.ErrorType tag2, double value)
                {
                }
            }
            """;
        var (diagnostics, trees) = TestHelpers.GetGeneratedTrees<TelemetryMetricGenerator>(input);
        using var scope = new AssertionScope();
        diagnostics.Should().BeEmpty();
        trees.Length.Should().Be(18);
        // tree 0 is the attributes
        trees[1].Should().Be(StandardGeneratedSource.TestMetricExtensions);
        trees[2].Should().Be(expectedTestMetricCollector);
        trees[3].Should().Be(expectedTestMetricCiVisibility);
        trees[4].Should().Be(expectedTestMetricInterface);
        trees[5].Should().Be(expectedTestMetricNull);
        trees[6].Should().Be(StandardGeneratedSource.TestCiMetricExtensions);
        trees[7].Should().Be(expectedTestCiMetricCollector);
        trees[8].Should().Be(expectedTestCiMetricCiVisibility);
        trees[9].Should().Be(expectedTestCiMetricInterface);
        trees[10].Should().Be(expectedTestCiMetricNull);
        trees[11].Should().Be(StandardGeneratedSource.TestSharedMetricExtensions);
        trees[12].Should().Be(expectedTestSharedMetricCollector);
        trees[13].Should().Be(expectedTestSharedMetricCiVisibility);
        trees[14].Should().Be(expectedTestSharedMetricInterface);
        trees[15].Should().Be(expectedTestSharedMetricNull);
        trees[16].Should().Be(StandardGeneratedSource.GetDistributionAggregateCollector());
        trees[17].Should().Be(StandardGeneratedSource.GetDistributionCiVisibilityAggregateCollector());
    }

    [Theory]
    [InlineData(@"null")]
    [InlineData("\"\"")]
    public void CantUseAnEmptyMetricType(string metricType)
    {
        var input = $$"""
            using Datadog.Trace.SourceGenerators;
            namespace MyTests.TestMetricNameSpace;

            [TelemetryMetricType({{metricType}})]
            public enum TestMetric
            { 
                [TelemetryMetric("some.metric", 1)]
                SomeMetric,
                [TelemetryMetric("another.metric", 2, false)]
                AnotherMetric,
                [TelemetryMetric("other.metric", 3, false, "ASM")]
                OtherMetric,
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TelemetryMetricGenerator>(input);
        diagnostics.Should().Contain(diag => diag.Id == MissingMetricTypeDiagnostic.Id);
    }

    [Theory]
    [InlineData(@"null")]
    [InlineData("\"\"")]
    public void CantUseAnEmptyMetricName(string name)
    {
        var input = $$"""
            using Datadog.Trace.SourceGenerators;
            namespace MyTests.TestMetricNameSpace;

            [TelemetryMetricType("Count")]
            public enum TestMetric
            { 
                [TelemetryMetric({{name}}, 1)]
                SomeMetric,
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TelemetryMetricGenerator>(input);
        diagnostics.Should().Contain(diag => diag.Id == RequiredValuesMissingDiagnostic.Id);
    }

    [Theory]
    [InlineData("[TelemetryMetric(\"some.metric\")]")]
    [InlineData("[TelemetryMetric(\"some.metric\", false)]")]
    [InlineData("[TelemetryMetric(\"some.metric\", false, \"ASM\")]")]
    [InlineData("[TelemetryMetric<LogLevel>(\"some.metric\")]")]
    [InlineData("[TelemetryMetric<LogLevel, ErrorType>(\"some.metric\")]")]
    public void CantUseDuplicateValues(string metricDefinition)
    {
        var input = $$"""
            using Datadog.Trace.SourceGenerators;
            using System.ComponentModel;

            namespace MyTests.TestMetricNameSpace;

            [TelemetryMetricType("distribution")]
            public enum TestMetric
            { 
                {{metricDefinition}}
                SomeMetric,
                {{metricDefinition}}
                OtherMetric,
            }

            public enum LogLevel
            {
                [Description("debug")] Debug,
                [Description("info")] Info,
                [Description("error")] Error,
            }

            public enum ErrorType
            {
                [Description("random")] Random,
                [Description("ducktyping")] DuckTyping,
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TelemetryMetricGenerator>(input);
        diagnostics.Should().Contain(diag => diag.Id == DuplicateMetricValueDiagnostic.Id);
    }

    public class StandardGeneratedSource
    {
        public const string TestMetricExtensions = Constants.FileHeader + """
            namespace MyTests.TestMetricNameSpace;
            internal static partial class TestMetricExtensions
            {
                /// <summary>
                /// The number of separate metrics in the <see cref="MyTests.TestMetricNameSpace.TestMetric" /> metric.
                /// </summary>
                public const int Length = 4;

                /// <summary>
                /// Gets the metric name for the provided metric
                /// </summary>
                /// <param name="metric">The metric to get the name for</param>
                /// <returns>The datadog metric name</returns>
                public static string GetName(this MyTests.TestMetricNameSpace.TestMetric metric)
                    => metric switch
                    {
                        MyTests.TestMetricNameSpace.TestMetric.ZeroTagMetric => "metric.zero",
                        MyTests.TestMetricNameSpace.TestMetric.OneTagMetric => "metric.one",
                        MyTests.TestMetricNameSpace.TestMetric.TwoTagMetric => "metric.two",
                        MyTests.TestMetricNameSpace.TestMetric.ZeroAgainTagMetric => "metric.zeroagain",
                        _ => null!,
                    };

                /// <summary>
                /// Gets whether the metric is a "common" metric, used by all tracers
                /// </summary>
                /// <param name="metric">The metric to check</param>
                /// <returns>True if the metric is a "common" metric, used by all languages</returns>
                public static bool IsCommon(this MyTests.TestMetricNameSpace.TestMetric metric)
                    => metric switch
                    {
                        MyTests.TestMetricNameSpace.TestMetric.ZeroTagMetric => false,
                        _ => true,
                    };

                /// <summary>
                /// Gets the custom namespace for the provided metric
                /// </summary>
                /// <param name="metric">The metric to get the name for</param>
                /// <returns>The datadog metric name</returns>
                public static string? GetNamespace(this MyTests.TestMetricNameSpace.TestMetric metric)
                    => metric switch
                    {
                        _ => null,
                    };
            }
            """;

        public const string TestCiMetricExtensions = Constants.FileHeader + """
            namespace MyTests.TestMetricNameSpace;
            internal static partial class TestCiMetricExtensions
            {
                /// <summary>
                /// The number of separate metrics in the <see cref="MyTests.TestMetricNameSpace.TestCiMetric" /> metric.
                /// </summary>
                public const int Length = 3;

                /// <summary>
                /// Gets the metric name for the provided metric
                /// </summary>
                /// <param name="metric">The metric to get the name for</param>
                /// <returns>The datadog metric name</returns>
                public static string GetName(this MyTests.TestMetricNameSpace.TestCiMetric metric)
                    => metric switch
                    {
                        MyTests.TestMetricNameSpace.TestCiMetric.CiZeroTagMetric => "ci.zero",
                        MyTests.TestMetricNameSpace.TestCiMetric.CiOneTagMetric => "ci.one",
                        MyTests.TestMetricNameSpace.TestCiMetric.CiTwoTagMetric => "ci.two",
                        _ => null!,
                    };

                /// <summary>
                /// Gets whether the metric is a "common" metric, used by all tracers
                /// </summary>
                /// <param name="metric">The metric to check</param>
                /// <returns>True if the metric is a "common" metric, used by all languages</returns>
                public static bool IsCommon(this MyTests.TestMetricNameSpace.TestCiMetric metric)
                    => metric switch
                    {
                        MyTests.TestMetricNameSpace.TestCiMetric.CiZeroTagMetric => false,
                        _ => true,
                    };

                /// <summary>
                /// Gets the custom namespace for the provided metric
                /// </summary>
                /// <param name="metric">The metric to get the name for</param>
                /// <returns>The datadog metric name</returns>
                public static string? GetNamespace(this MyTests.TestMetricNameSpace.TestCiMetric metric)
                    => metric switch
                    {
                        MyTests.TestMetricNameSpace.TestCiMetric.CiZeroTagMetric => "civisibility",
                        MyTests.TestMetricNameSpace.TestCiMetric.CiOneTagMetric => "civisibility",
                        MyTests.TestMetricNameSpace.TestCiMetric.CiTwoTagMetric => "civisibility",
                        _ => null,
                    };
            }
            """;

        public const string TestSharedMetricExtensions = Constants.FileHeader + """
            namespace MyTests.TestMetricNameSpace;
            internal static partial class TestSharedMetricExtensions
            {
                /// <summary>
                /// The number of separate metrics in the <see cref="MyTests.TestMetricNameSpace.TestSharedMetric" /> metric.
                /// </summary>
                public const int Length = 3;

                /// <summary>
                /// Gets the metric name for the provided metric
                /// </summary>
                /// <param name="metric">The metric to get the name for</param>
                /// <returns>The datadog metric name</returns>
                public static string GetName(this MyTests.TestMetricNameSpace.TestSharedMetric metric)
                    => metric switch
                    {
                        MyTests.TestMetricNameSpace.TestSharedMetric.SharedZeroTagMetric => "shared.zero",
                        MyTests.TestMetricNameSpace.TestSharedMetric.SharedOneTagMetric => "shared.one",
                        MyTests.TestMetricNameSpace.TestSharedMetric.SharedTwoTagMetric => "shared.two",
                        _ => null!,
                    };

                /// <summary>
                /// Gets whether the metric is a "common" metric, used by all tracers
                /// </summary>
                /// <param name="metric">The metric to check</param>
                /// <returns>True if the metric is a "common" metric, used by all languages</returns>
                public static bool IsCommon(this MyTests.TestMetricNameSpace.TestSharedMetric metric)
                    => metric switch
                    {
                        MyTests.TestMetricNameSpace.TestSharedMetric.SharedZeroTagMetric => false,
                        _ => true,
                    };

                /// <summary>
                /// Gets the custom namespace for the provided metric
                /// </summary>
                /// <param name="metric">The metric to get the name for</param>
                /// <returns>The datadog metric name</returns>
                public static string? GetNamespace(this MyTests.TestMetricNameSpace.TestSharedMetric metric)
                    => metric switch
                    {
                        _ => null,
                    };
            }
            """;

        public static string GetMetricAggregateCollector(string metricType) => Constants.FileHeader + $$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using Datadog.Trace.Telemetry.Metrics;
            using Datadog.Trace.Util;

            namespace Datadog.Trace.Telemetry;

            internal partial class MetricsTelemetryCollector
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
                        metricData = GetMetricData(aggregated.PublicApiCounts, aggregated.TestMetric, aggregated.TestSharedMetric);
                        distributionData = (null);
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
                        AggregateMetric(buffer.TestMetric, timestamp, aggregated.TestMetric);
                        AggregateMetric(buffer.TestSharedMetric, timestamp, aggregated.TestSharedMetric);
                    }

                    // prepare the buffer for next time
                    buffer.Clear();
                    Interlocked.Exchange(ref _reserveBuffer, buffer);
                }

                /// <summary>
                /// Loop through the aggregated data, looking for any metrics that have points
                /// </summary>
                private List<MetricData>? GetMetricData(AggregatedMetric[] publicApis, AggregatedMetric[] testmetric, AggregatedMetric[] testsharedmetric)
                {
                    var apiLength = publicApis.Count(x => x.HasValues);
                    var testmetricLength = testmetric.Count(x => x.HasValues);
                    var testsharedmetricLength = testsharedmetric.Count(x => x.HasValues);

                    var totalLength = apiLength + testmetricLength + testsharedmetricLength;
                    if (totalLength == 0)
                    {
                        return null;
                    }

                    var data = new List<MetricData>(totalLength);

                    if (apiLength > 0)
                    {
                        AddPublicApiMetricData(publicApis, data);
                    }

                    if (testmetricLength > 0)
                    {
                        AddMetricData("{{metricType}}", testmetric, data, TestMetricEntryCounts, GetTestMetricDetails);
                    }

                    if (testsharedmetricLength > 0)
                    {
                        AddMetricData("{{metricType}}", testsharedmetric, data, TestSharedMetricEntryCounts, GetTestSharedMetricDetails);
                    }

                    return data;
                }

                private List<DistributionMetricData>? GetDistributionData()
                {

                    var totalLength = 0;
                    if (totalLength == 0)
                    {
                        return null;
                    }

                    var data = new List<DistributionMetricData>(totalLength);

                    return data;
                }

                private static MetricDetails GetTestMetricDetails(int i)
                {
                    var metric = (TestMetric)i;
                    return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
                }

                private static MetricDetails GetTestSharedMetricDetails(int i)
                {
                    var metric = (TestSharedMetric)i;
                    return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
                }

                private class AggregatedMetrics
                {
                    public readonly AggregatedMetric[] PublicApiCounts;
                    public readonly AggregatedMetric[] TestMetric;
                    public readonly AggregatedMetric[] TestSharedMetric;

                    public AggregatedMetrics()
                    {
                        PublicApiCounts = GetPublicApiCountBuffer();
                        TestMetric = GetTestMetricBuffer();
                        TestSharedMetric = GetTestSharedMetricBuffer();
                    }
                }

                protected class MetricBuffer
                {
                    public readonly int[] PublicApiCounts;
                    public readonly int[] TestMetric;
                    public readonly int[] TestSharedMetric;

                    public MetricBuffer()
                    {
                        PublicApiCounts = new int[PublicApiUsageExtensions.Length];
                        TestMetric = new int[TestMetricLength];
                        TestSharedMetric = new int[TestSharedMetricLength];
                    }

                    public void Clear()
                    {
                        for (var i = 0; i < PublicApiCounts.Length; i++)
                        {
                            PublicApiCounts[i] = 0;
                        }

                        for (var i = 0; i < TestMetric.Length; i++)
                        {
                            TestMetric[i] = 0;
                        }

                        for (var i = 0; i < TestSharedMetric.Length; i++)
                        {
                            TestSharedMetric[i] = 0;
                        }
                    }
                }
            }
            """;

        public static string GetMetricCiVisibilityAggregateCollector(string metricType) => Constants.FileHeader + $$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using Datadog.Trace.Telemetry.Metrics;
            using Datadog.Trace.Util;

            namespace Datadog.Trace.Telemetry;

            internal partial class CiVisibilityMetricsTelemetryCollector
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
                        metricData = GetMetricData(aggregated.PublicApiCounts, aggregated.TestCiMetric, aggregated.TestSharedMetric);
                        distributionData = (null);
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
                        AggregateMetric(buffer.TestCiMetric, timestamp, aggregated.TestCiMetric);
                        AggregateMetric(buffer.TestSharedMetric, timestamp, aggregated.TestSharedMetric);
                    }

                    // prepare the buffer for next time
                    buffer.Clear();
                    Interlocked.Exchange(ref _reserveBuffer, buffer);
                }

                /// <summary>
                /// Loop through the aggregated data, looking for any metrics that have points
                /// </summary>
                private List<MetricData>? GetMetricData(AggregatedMetric[] publicApis, AggregatedMetric[] testcimetric, AggregatedMetric[] testsharedmetric)
                {
                    var apiLength = publicApis.Count(x => x.HasValues);
                    var testcimetricLength = testcimetric.Count(x => x.HasValues);
                    var testsharedmetricLength = testsharedmetric.Count(x => x.HasValues);

                    var totalLength = apiLength + testcimetricLength + testsharedmetricLength;
                    if (totalLength == 0)
                    {
                        return null;
                    }

                    var data = new List<MetricData>(totalLength);

                    if (apiLength > 0)
                    {
                        AddPublicApiMetricData(publicApis, data);
                    }

                    if (testcimetricLength > 0)
                    {
                        AddMetricData("{{metricType}}", testcimetric, data, TestCiMetricEntryCounts, GetTestCiMetricDetails);
                    }

                    if (testsharedmetricLength > 0)
                    {
                        AddMetricData("{{metricType}}", testsharedmetric, data, TestSharedMetricEntryCounts, GetTestSharedMetricDetails);
                    }

                    return data;
                }

                private List<DistributionMetricData>? GetDistributionData()
                {

                    var totalLength = 0;
                    if (totalLength == 0)
                    {
                        return null;
                    }

                    var data = new List<DistributionMetricData>(totalLength);

                    return data;
                }

                private static MetricDetails GetTestCiMetricDetails(int i)
                {
                    var metric = (TestCiMetric)i;
                    return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
                }

                private static MetricDetails GetTestSharedMetricDetails(int i)
                {
                    var metric = (TestSharedMetric)i;
                    return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
                }

                private class AggregatedMetrics
                {
                    public readonly AggregatedMetric[] PublicApiCounts;
                    public readonly AggregatedMetric[] TestCiMetric;
                    public readonly AggregatedMetric[] TestSharedMetric;

                    public AggregatedMetrics()
                    {
                        PublicApiCounts = GetPublicApiCountBuffer();
                        TestCiMetric = GetTestCiMetricBuffer();
                        TestSharedMetric = GetTestSharedMetricBuffer();
                    }
                }

                protected class MetricBuffer
                {
                    public readonly int[] PublicApiCounts;
                    public readonly int[] TestCiMetric;
                    public readonly int[] TestSharedMetric;

                    public MetricBuffer()
                    {
                        PublicApiCounts = new int[PublicApiUsageExtensions.Length];
                        TestCiMetric = new int[TestCiMetricLength];
                        TestSharedMetric = new int[TestSharedMetricLength];
                    }

                    public void Clear()
                    {
                        for (var i = 0; i < PublicApiCounts.Length; i++)
                        {
                            PublicApiCounts[i] = 0;
                        }

                        for (var i = 0; i < TestCiMetric.Length; i++)
                        {
                            TestCiMetric[i] = 0;
                        }

                        for (var i = 0; i < TestSharedMetric.Length; i++)
                        {
                            TestSharedMetric[i] = 0;
                        }
                    }
                }
            }
            """;

        public static string GetDistributionAggregateCollector() => Constants.FileHeader + $$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using Datadog.Trace.Telemetry.Metrics;
            using Datadog.Trace.Util;

            namespace Datadog.Trace.Telemetry;

            internal partial class MetricsTelemetryCollector
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
                        metricData = GetMetricData(aggregated.PublicApiCounts);
                        distributionData = GetDistributionData(aggregated.TestMetric, aggregated.TestSharedMetric);
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
                        AggregateDistribution(buffer.TestMetric, aggregated.TestMetric);
                        AggregateDistribution(buffer.TestSharedMetric, aggregated.TestSharedMetric);
                    }

                    // prepare the buffer for next time
                    buffer.Clear();
                    Interlocked.Exchange(ref _reserveBuffer, buffer);
                }

                /// <summary>
                /// Loop through the aggregated data, looking for any metrics that have points
                /// </summary>
                private List<MetricData>? GetMetricData(AggregatedMetric[] publicApis)
                {
                    var apiLength = publicApis.Count(x => x.HasValues);

                    var totalLength = apiLength;
                    if (totalLength == 0)
                    {
                        return null;
                    }

                    var data = new List<MetricData>(totalLength);

                    if (apiLength > 0)
                    {
                        AddPublicApiMetricData(publicApis, data);
                    }

                    return data;
                }

                private List<DistributionMetricData>? GetDistributionData(AggregatedDistribution[] testmetric, AggregatedDistribution[] testsharedmetric)
                {
                    var testmetricLength = testmetric.Count(x => x.HasValues);
                    var testsharedmetricLength = testsharedmetric.Count(x => x.HasValues);

                    var totalLength = 0 + testmetricLength + testsharedmetricLength;
                    if (totalLength == 0)
                    {
                        return null;
                    }

                    var data = new List<DistributionMetricData>(totalLength);
            
                    if (testmetricLength > 0)
                    {
                        AddDistributionData(testmetric, data, TestMetricEntryCounts, GetTestMetricDetails);
                    }

                    if (testsharedmetricLength > 0)
                    {
                        AddDistributionData(testsharedmetric, data, TestSharedMetricEntryCounts, GetTestSharedMetricDetails);
                    }

                    return data;
                }

                private static MetricDetails GetTestMetricDetails(int i)
                {
                    var metric = (TestMetric)i;
                    return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
                }

                private static MetricDetails GetTestSharedMetricDetails(int i)
                {
                    var metric = (TestSharedMetric)i;
                    return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
                }

                private class AggregatedMetrics
                {
                    public readonly AggregatedMetric[] PublicApiCounts;
                    public readonly AggregatedDistribution[] TestMetric;
                    public readonly AggregatedDistribution[] TestSharedMetric;

                    public AggregatedMetrics()
                    {
                        PublicApiCounts = GetPublicApiCountBuffer();
                        TestMetric = GetTestMetricBuffer();
                        TestSharedMetric = GetTestSharedMetricBuffer();
                    }
                }

                protected class MetricBuffer
                {
                    public readonly int[] PublicApiCounts;
                    public readonly BoundedConcurrentQueue<double>[] TestMetric;
                    public readonly BoundedConcurrentQueue<double>[] TestSharedMetric;

                    public MetricBuffer()
                    {
                        PublicApiCounts = new int[PublicApiUsageExtensions.Length];
                        TestMetric = new BoundedConcurrentQueue<double>[TestMetricLength];

                        for (var i = TestMetric.Length - 1; i >= 0; i--)
                        {
                            TestMetric[i] = new BoundedConcurrentQueue<double>(queueLimit: 1000);
                        }

                        TestSharedMetric = new BoundedConcurrentQueue<double>[TestSharedMetricLength];

                        for (var i = TestSharedMetric.Length - 1; i >= 0; i--)
                        {
                            TestSharedMetric[i] = new BoundedConcurrentQueue<double>(queueLimit: 1000);
                        }

                    }

                    public void Clear()
                    {
                        for (var i = 0; i < PublicApiCounts.Length; i++)
                        {
                            PublicApiCounts[i] = 0;
                        }

                        for (var i = 0; i < TestMetric.Length; i++)
                        {
                            while (TestMetric[i].TryDequeue(out _)) { }
                        }

                        for (var i = 0; i < TestSharedMetric.Length; i++)
                        {
                            while (TestSharedMetric[i].TryDequeue(out _)) { }
                        }
                    }
                }
            }
            """;

        public static string GetDistributionCiVisibilityAggregateCollector() => Constants.FileHeader + """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using Datadog.Trace.Telemetry.Metrics;
            using Datadog.Trace.Util;

            namespace Datadog.Trace.Telemetry;

            internal partial class CiVisibilityMetricsTelemetryCollector
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
                        metricData = GetMetricData(aggregated.PublicApiCounts);
                        distributionData = GetDistributionData(aggregated.TestCiMetric, aggregated.TestSharedMetric);
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
                        AggregateDistribution(buffer.TestCiMetric, aggregated.TestCiMetric);
                        AggregateDistribution(buffer.TestSharedMetric, aggregated.TestSharedMetric);
                    }

                    // prepare the buffer for next time
                    buffer.Clear();
                    Interlocked.Exchange(ref _reserveBuffer, buffer);
                }

                /// <summary>
                /// Loop through the aggregated data, looking for any metrics that have points
                /// </summary>
                private List<MetricData>? GetMetricData(AggregatedMetric[] publicApis)
                {
                    var apiLength = publicApis.Count(x => x.HasValues);

                    var totalLength = apiLength;
                    if (totalLength == 0)
                    {
                        return null;
                    }

                    var data = new List<MetricData>(totalLength);

                    if (apiLength > 0)
                    {
                        AddPublicApiMetricData(publicApis, data);
                    }

                    return data;
                }

                private List<DistributionMetricData>? GetDistributionData(AggregatedDistribution[] testcimetric, AggregatedDistribution[] testsharedmetric)
                {
                    var testcimetricLength = testcimetric.Count(x => x.HasValues);
                    var testsharedmetricLength = testsharedmetric.Count(x => x.HasValues);

                    var totalLength = 0 + testcimetricLength + testsharedmetricLength;
                    if (totalLength == 0)
                    {
                        return null;
                    }

                    var data = new List<DistributionMetricData>(totalLength);
            
                    if (testcimetricLength > 0)
                    {
                        AddDistributionData(testcimetric, data, TestCiMetricEntryCounts, GetTestCiMetricDetails);
                    }

                    if (testsharedmetricLength > 0)
                    {
                        AddDistributionData(testsharedmetric, data, TestSharedMetricEntryCounts, GetTestSharedMetricDetails);
                    }

                    return data;
                }

                private static MetricDetails GetTestCiMetricDetails(int i)
                {
                    var metric = (TestCiMetric)i;
                    return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
                }

                private static MetricDetails GetTestSharedMetricDetails(int i)
                {
                    var metric = (TestSharedMetric)i;
                    return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
                }

                private class AggregatedMetrics
                {
                    public readonly AggregatedMetric[] PublicApiCounts;
                    public readonly AggregatedDistribution[] TestCiMetric;
                    public readonly AggregatedDistribution[] TestSharedMetric;

                    public AggregatedMetrics()
                    {
                        PublicApiCounts = GetPublicApiCountBuffer();
                        TestCiMetric = GetTestCiMetricBuffer();
                        TestSharedMetric = GetTestSharedMetricBuffer();
                    }
                }

                protected class MetricBuffer
                {
                    public readonly int[] PublicApiCounts;
                    public readonly BoundedConcurrentQueue<double>[] TestCiMetric;
                    public readonly BoundedConcurrentQueue<double>[] TestSharedMetric;

                    public MetricBuffer()
                    {
                        PublicApiCounts = new int[PublicApiUsageExtensions.Length];
                        TestCiMetric = new BoundedConcurrentQueue<double>[TestCiMetricLength];

                        for (var i = TestCiMetric.Length - 1; i >= 0; i--)
                        {
                            TestCiMetric[i] = new BoundedConcurrentQueue<double>(queueLimit: 1000);
                        }

                        TestSharedMetric = new BoundedConcurrentQueue<double>[TestSharedMetricLength];

                        for (var i = TestSharedMetric.Length - 1; i >= 0; i--)
                        {
                            TestSharedMetric[i] = new BoundedConcurrentQueue<double>(queueLimit: 1000);
                        }

                    }

                    public void Clear()
                    {
                        for (var i = 0; i < PublicApiCounts.Length; i++)
                        {
                            PublicApiCounts[i] = 0;
                        }

                        for (var i = 0; i < TestCiMetric.Length; i++)
                        {
                            while (TestCiMetric[i].TryDequeue(out _)) { }
                        }

                        for (var i = 0; i < TestSharedMetric.Length; i++)
                        {
                            while (TestSharedMetric[i].TryDequeue(out _)) { }
                        }
                    }
                }
            }
            """;

        public static string GetTestMetricBuffers(bool isDistribution = false)
        {
            var aggregation = isDistribution ? "AggregatedDistribution" : "AggregatedMetric";
            return $$"""
                private const int TestMetricLength = 18;

                /// <summary>
                /// Creates the buffer for the <see cref="MyTests.TestMetricNameSpace.TestMetric" /> values.
                /// </summary>
                private static {{aggregation}}[] GetTestMetricBuffer()
                    => new {{aggregation}}[]
                    {
                        // metric.zero, index = 0
                        new(null),
                        // metric.one, index = 1
                        new(null),
                        new(new[] { "debug" }),
                        new(new[] { "info" }),
                        new(new[] { "error" }),
                        // metric.two, index = 5
                        new(null),
                        new(new[] { "random" }),
                        new(new[] { "ducktyping", "othertag", "somethingelse" }),
                        new(new[] { "debug" }),
                        new(new[] { "debug", "random" }),
                        new(new[] { "debug", "ducktyping", "othertag", "somethingelse" }),
                        new(new[] { "info" }),
                        new(new[] { "info", "random" }),
                        new(new[] { "info", "ducktyping", "othertag", "somethingelse" }),
                        new(new[] { "error" }),
                        new(new[] { "error", "random" }),
                        new(new[] { "error", "ducktyping", "othertag", "somethingelse" }),
                        // metric.zeroagain, index = 17
                        new(null),
                    };

                /// <summary>
                /// Gets an array of metric counts, indexed by integer value of the <see cref="MyTests.TestMetricNameSpace.TestMetric" />.
                /// Each value represents the number of unique entries in the buffer returned by <see cref="GetTestMetricBuffer()" />
                /// It is equal to the cardinality of the tag combinations (or 1 if there are no tags)
                /// </summary>
                private static int[] TestMetricEntryCounts { get; }
                    = new int[]{ 1, 4, 12, 1, };
            """;
        }

        public static string GetTestCiMetricBuffers(bool isDistribution = false)
        {
            var aggregation = isDistribution ? "AggregatedDistribution" : "AggregatedMetric";
            return $$"""
                private const int TestCiMetricLength = 17;

                /// <summary>
                /// Creates the buffer for the <see cref="MyTests.TestMetricNameSpace.TestCiMetric" /> values.
                /// </summary>
                private static {{aggregation}}[] GetTestCiMetricBuffer()
                    => new {{aggregation}}[]
                    {
                        // ci.zero, index = 0
                        new(null),
                        // ci.one, index = 1
                        new(null),
                        new(new[] { "debug" }),
                        new(new[] { "info" }),
                        new(new[] { "error" }),
                        // ci.two, index = 5
                        new(null),
                        new(new[] { "random" }),
                        new(new[] { "ducktyping", "othertag", "somethingelse" }),
                        new(new[] { "debug" }),
                        new(new[] { "debug", "random" }),
                        new(new[] { "debug", "ducktyping", "othertag", "somethingelse" }),
                        new(new[] { "info" }),
                        new(new[] { "info", "random" }),
                        new(new[] { "info", "ducktyping", "othertag", "somethingelse" }),
                        new(new[] { "error" }),
                        new(new[] { "error", "random" }),
                        new(new[] { "error", "ducktyping", "othertag", "somethingelse" }),
                    };

                /// <summary>
                /// Gets an array of metric counts, indexed by integer value of the <see cref="MyTests.TestMetricNameSpace.TestCiMetric" />.
                /// Each value represents the number of unique entries in the buffer returned by <see cref="GetTestCiMetricBuffer()" />
                /// It is equal to the cardinality of the tag combinations (or 1 if there are no tags)
                /// </summary>
                private static int[] TestCiMetricEntryCounts { get; }
                    = new int[]{ 1, 4, 12, };
            """;
        }

        public static string GetTestSharedMetricBuffers(bool isDistribution = false)
        {
            var aggregation = isDistribution ? "AggregatedDistribution" : "AggregatedMetric";
            return $$"""
                private const int TestSharedMetricLength = 17;

                /// <summary>
                /// Creates the buffer for the <see cref="MyTests.TestMetricNameSpace.TestSharedMetric" /> values.
                /// </summary>
                private static {{aggregation}}[] GetTestSharedMetricBuffer()
                    => new {{aggregation}}[]
                    {
                        // shared.zero, index = 0
                        new(null),
                        // shared.one, index = 1
                        new(null),
                        new(new[] { "debug" }),
                        new(new[] { "info" }),
                        new(new[] { "error" }),
                        // shared.two, index = 5
                        new(null),
                        new(new[] { "random" }),
                        new(new[] { "ducktyping", "othertag", "somethingelse" }),
                        new(new[] { "debug" }),
                        new(new[] { "debug", "random" }),
                        new(new[] { "debug", "ducktyping", "othertag", "somethingelse" }),
                        new(new[] { "info" }),
                        new(new[] { "info", "random" }),
                        new(new[] { "info", "ducktyping", "othertag", "somethingelse" }),
                        new(new[] { "error" }),
                        new(new[] { "error", "random" }),
                        new(new[] { "error", "ducktyping", "othertag", "somethingelse" }),
                    };

                /// <summary>
                /// Gets an array of metric counts, indexed by integer value of the <see cref="MyTests.TestMetricNameSpace.TestSharedMetric" />.
                /// Each value represents the number of unique entries in the buffer returned by <see cref="GetTestSharedMetricBuffer()" />
                /// It is equal to the cardinality of the tag combinations (or 1 if there are no tags)
                /// </summary>
                private static int[] TestSharedMetricEntryCounts { get; }
                    = new int[]{ 1, 4, 12, };
            """;
        }

        public static string GetStandardEnums(string metricType)
            =>  $$"""
            using Datadog.Trace.SourceGenerators;
            using System.ComponentModel;

            namespace MyTests.TestMetricNameSpace;

            [TelemetryMetricType("{{metricType}}")]
            public enum TestMetric
            { 
                [TelemetryMetric("metric.zero", false)]
                ZeroTagMetric,

                [TelemetryMetric<LogLevel>("metric.one")]
                OneTagMetric,

                [TelemetryMetric<LogLevel, ErrorType>("metric.two")]
                TwoTagMetric,

                [TelemetryMetric("metric.zeroagain")]
                ZeroAgainTagMetric,
            }

            [TelemetryMetricType("{{metricType}}", true, false)]
            public enum TestCiMetric
            { 
                [TelemetryMetric("ci.zero", false, "civisibility")]
                CiZeroTagMetric,

                [TelemetryMetric<LogLevel>("ci.one", true, "civisibility")]
                CiOneTagMetric,
            
                [TelemetryMetric<LogLevel, ErrorType>("ci.two", true, "civisibility")]
                CiTwoTagMetric,
            }

            [TelemetryMetricType("{{metricType}}", true, true)]
            public enum TestSharedMetric
            { 
                [TelemetryMetric("shared.zero", false)]
                SharedZeroTagMetric,
            
                [TelemetryMetric<LogLevel>("shared.one")]
                SharedOneTagMetric,
            
                [TelemetryMetric<LogLevel, ErrorType>("shared.two")]
                SharedTwoTagMetric,
            }

            public enum LogLevel
            {
                [Description("")] None,
                [Description("debug")] Debug,
                [Description("info")] Info,
                [Description("error")] Error,
            }

            public enum ErrorType
            {
                [Description("")] None,
                [Description("random;;")] Random,
                [Description("ducktyping;;othertag;somethingelse")] DuckTyping,
            }
            """;
    }
}
