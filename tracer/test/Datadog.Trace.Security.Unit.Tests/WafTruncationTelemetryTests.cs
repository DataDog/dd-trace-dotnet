// <copyright file="WafTruncationTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

// replaces the global metrics collector
[Collection(nameof(SecuritySequentialTests))]
public class WafTruncationTelemetryTests : WafLibraryRequiredTest
{
    [SkippableTheory]
    [CombinatorialData]
    public async Task GivenAWafRunWithEveryKindOfTruncation_WhenItRuns_ThenOneInputTruncatedPointHoldsAllTheFlags(bool useUnsafeEncoder)
    {
        var metrics = await RecordAsync(useUnsafeEncoder, CreateTruncatedPayload());

        metrics.Should().ContainSingle(m => m.Name == "waf.input_truncated")
               .Which.Should().Be(("waf.input_truncated", "truncation_reason:7", 1));
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task GivenAWafRunWithinTheLimits_WhenItRuns_ThenNoInputTruncatedPointIsReported(bool useUnsafeEncoder)
    {
        var metrics = await RecordAsync(useUnsafeEncoder, new Dictionary<string, object> { { AddressesConstants.RequestBody, "value" } });

        metrics.Should().NotContain(m => m.Name == "waf.input_truncated");
    }

    [SkippableTheory]
    [CombinatorialData]
    public void GivenTruncatedRuns_WhenTheWebSpanCloses_ThenTheLargestTruncationsOfTheRequestAreSetOnIt(bool useUnsafeEncoder)
    {
        using var context = CreateWaf(useUnsafeEncoder).Waf!.CreateContext(out _)!;
        var requestContext = new AppSecRequestContext();

        requestContext.AddTruncation(context.Run(CreateTruncatedPayload(), TimeoutMicroSeconds)!.Truncation);
        requestContext.AddTruncation(context.Run(new Dictionary<string, object> { { AddressesConstants.RequestQuery, new string('b', 6000) } }, TimeoutMicroSeconds)!.Truncation);
        var span = new Span(new SpanContext(SpanContext.None, null, "Test"), DateTimeOffset.Now);
        requestContext.CloseWebSpan(span);

        span.GetMetric(Metrics.AppSecTruncatedStringLength).Should().Be(6000);
        span.GetMetric(Metrics.AppSecTruncatedContainerSize).Should().Be(300);
        span.GetMetric(Metrics.AppSecTruncatedContainerDepth).Should().Be(WafConstants.MaxContainerDepth + 1);
    }

    [Fact]
    public void GivenAWebSpanWithoutTruncation_WhenItCloses_ThenNoTruncationMetricIsSet()
    {
        var span = new Span(new SpanContext(SpanContext.None, null, "Test"), DateTimeOffset.Now);

        new AppSecRequestContext().CloseWebSpan(span);

        span.GetMetric(Metrics.AppSecTruncatedStringLength).Should().BeNull();
        span.GetMetric(Metrics.AppSecTruncatedContainerSize).Should().BeNull();
        span.GetMetric(Metrics.AppSecTruncatedContainerDepth).Should().BeNull();
    }

    [Fact]
    public void GivenTheTruncationReasonValues_WhenTheirTagsAreRead_ThenTheyAreTheFlagsOfTheRfc()
    {
        // TruncationInfo computes the value as the flags minus one
        var values = (MetricTags.TruncationReason[])Enum.GetValues(typeof(MetricTags.TruncationReason));

        values.Select(v => typeof(MetricTags.TruncationReason).GetField(v.ToString())!.GetCustomAttribute<DescriptionAttribute>()!.Description)
              .Should().Equal(Enumerable.Range(1, 7).Select(flags => $"truncation_reason:{flags}"));
        values.Select(v => (int)v).Should().Equal(Enumerable.Range(0, 7));
    }

    private static Dictionary<string, object> CreateTruncatedPayload()
    {
        object deep = new Dictionary<string, object> { { "value", "a" } };
        for (var i = 0; i < 25; i++)
        {
            deep = new Dictionary<string, object> { { "a", deep } };
        }

        return new Dictionary<string, object>
        {
            {
                AddressesConstants.RequestBody,
                new Dictionary<string, object>
                {
                    { "deepObject", deep },
                    { "longValue", new string('a', 5000) },
                    { "largeObject", Enumerable.Range(0, 300).ToDictionary(i => $"key{i}", i => (object)$"value{i}") },
                }
            },
        };
    }

    private async Task<List<(string Name, string Tags, int Points)>> RecordAsync(bool useUnsafeEncoder, Dictionary<string, object> args)
    {
        using var context = CreateWaf(useUnsafeEncoder).Waf!.CreateContext(out _)!;
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var previousMetrics = TelemetryFactory.SetMetricsForTesting(collector);

        try
        {
            context.Run(args, TimeoutMicroSeconds);
        }
        finally
        {
            TelemetryFactory.SetMetricsForTesting(previousMetrics);
        }

        await collector.DisposeAsync();
        return collector.GetMetrics().Metrics?
                        .Select(m => (m.Metric, string.Join(",", m.Tags ?? []), m.Points.Sum(p => (int)p.Value)))
                        .ToList()
            ?? [];
    }
}
