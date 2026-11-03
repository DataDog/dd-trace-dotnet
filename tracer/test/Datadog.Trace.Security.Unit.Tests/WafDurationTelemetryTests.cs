// <copyright file="WafDurationTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class WafDurationTelemetryTests : WafLibraryRequiredTest
{
    private static readonly Dictionary<string, object> RequestArgs = new()
    {
        { AddressesConstants.RequestUriRaw, "http://localhost:54587/" },
        { AddressesConstants.RequestMethod, "GET" },
    };

    private static readonly Dictionary<string, object> RaspArgs = new()
    {
        { AddressesConstants.FileAccess, "../../../../../../../../../etc/passwd" },
    };

    [Fact]
    public async Task GivenWafAndRaspRuns_WhenTheDurationsAreRecorded_ThenEachScopeReportsItsOwnTotals()
    {
        using var context = CreateContext();
        context.Run(RequestArgs, TimeoutMicroSeconds);
        var rasp = context.RunWithEphemeral(RaspArgs, TimeoutMicroSeconds, isRasp: true, out _)!;
        var waf = context.Run(RequestArgs, TimeoutMicroSeconds)!;

        var distributions = await RecordAsync(context);

        distributions.Should().HaveCount(4);
        distributions["waf.duration"].Should().Equal((double)waf.AggregatedTotalRuntime);
        distributions["waf.duration_ext"].Should().Equal((double)waf.AggregatedTotalRuntimeWithBindings);
        distributions["rasp.duration"].Should().Equal((double)rasp.AggregatedTotalRuntimeRasp);
        distributions["rasp.duration_ext"].Should().Equal((double)rasp.AggregatedTotalRuntimeWithBindingsRasp);
        distributions["waf.duration_ext"].Single().Should().BeGreaterOrEqualTo(distributions["waf.duration"].Single());
        distributions["rasp.duration_ext"].Single().Should().BeGreaterOrEqualTo(distributions["rasp.duration"].Single());
    }

    [Fact]
    public async Task GivenOnlyWafRuns_WhenTheDurationsAreRecorded_ThenNoRaspDurationIsReported()
    {
        using var context = CreateContext();
        context.Run(RequestArgs, TimeoutMicroSeconds);

        var distributions = await RecordAsync(context);

        distributions.Keys.Should().BeEquivalentTo("waf.duration", "waf.duration_ext");
    }

    [Fact]
    public async Task GivenNoRun_WhenTheDurationsAreRecorded_ThenNothingIsReported()
    {
        using var context = CreateContext();

        var distributions = await RecordAsync(context);

        distributions.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenARunAfterTheContextIsDisposed_WhenTheDurationsAreRecorded_ThenItDoesNotCount()
    {
        var context = CreateContext();
        var waf = context.Run(RequestArgs, TimeoutMicroSeconds)!;
        context.Dispose();
        context.Run(RequestArgs, TimeoutMicroSeconds).Should().BeNull();

        var distributions = await RecordAsync(context);

        distributions["waf.duration_ext"].Should().Equal((double)waf.AggregatedTotalRuntimeWithBindings);
    }

    private static async Task<Dictionary<string, List<double>>> RecordAsync(Context context)
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        context.RecordDurationTelemetry(collector);
        await collector.DisposeAsync();

        return collector.GetMetrics().Distributions?
                        .ToDictionary(d => d.Metric, d => d.Points)
            ?? [];
    }

    private Context CreateContext()
    {
        var waf = CreateWaf(ruleFile: "rasp-rule-set.json").Waf!;
        return (Context)waf.CreateContext(out _)!;
    }
}
