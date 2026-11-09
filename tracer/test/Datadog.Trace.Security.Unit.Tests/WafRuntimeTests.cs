// <copyright file="WafRuntimeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

// The totals measured by Context can only be smaller than the wall clock measured around the calls that ran,
// which gives upper bounds that don't depend on how long the WAF takes.
public class WafRuntimeTests : WafLibraryRequiredTest
{
    private const int PauseMilliseconds = 50;

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
    public void GivenInterleavedWafAndRaspRuns_WhenTheyRun_ThenEachScopeOnlyCountsItsOwnRuns()
    {
        WarmUp();
        using var context = CreateContext();

        // a large input makes the WAF run much longer than the RASP one, so that mixing them breaks the RASP bound
        var (waf1, waf1Elapsed) = Measure(() => context.Run(CreateLargeRequestArgs(), TimeoutMicroSeconds));
        Thread.Sleep(PauseMilliseconds);
        var (rasp, raspElapsed) = Measure(() => context.RunWithEphemeral(RaspArgs, TimeoutMicroSeconds, isRasp: true, out _));
        Thread.Sleep(PauseMilliseconds);
        var (waf2, waf2Elapsed) = Measure(() => context.Run(RequestArgs, TimeoutMicroSeconds));

        rasp.AggregatedTotalRuntimeWithBindingsRasp.Should().BeGreaterThan(0).And.BeLessOrEqualTo(raspElapsed);
        rasp.AggregatedTotalRuntimeRasp.Should().BeLessOrEqualTo(rasp.AggregatedTotalRuntimeWithBindingsRasp);
        rasp.AggregatedTotalRuntimeWithBindings.Should().Be(0);

        waf2.AggregatedTotalRuntimeWithBindings.Should().BeGreaterOrEqualTo(waf1.AggregatedTotalRuntimeWithBindings).And.BeLessOrEqualTo(waf1Elapsed + waf2Elapsed);
        waf2.AggregatedTotalRuntime.Should().BeGreaterOrEqualTo(waf1.AggregatedTotalRuntime).And.BeLessOrEqualTo(waf2.AggregatedTotalRuntimeWithBindings);
        waf2.AggregatedTotalRuntimeWithBindingsRasp.Should().Be(0);
    }

    [Fact]
    public void GivenRejectedRuns_WhenTheNextRunsComplete_ThenTheTimeAfterTheRejectedRunsIsNotCounted()
    {
        using var context = CreateContext();

        var (waf1, waf1Elapsed) = Measure(() => context.Run(RequestArgs, TimeoutMicroSeconds));

        context.RunWithEphemeral(new Dictionary<string, object>(), TimeoutMicroSeconds, isRasp: false, out var wafOutcome).Should().BeNull();
        wafOutcome.Should().Be(WafOutcome.BindingFailed);
        context.RunWithEphemeral(new Dictionary<string, object>(), TimeoutMicroSeconds, isRasp: true, out var raspOutcome).Should().BeNull();
        raspOutcome.Should().Be(WafOutcome.BindingFailed);
        Thread.Sleep(PauseMilliseconds);

        var (waf2, waf2Elapsed) = Measure(() => context.Run(RequestArgs, TimeoutMicroSeconds));
        var (rasp, raspElapsed) = Measure(() => context.RunWithEphemeral(RaspArgs, TimeoutMicroSeconds, isRasp: true, out _));

        waf2.AggregatedTotalRuntimeWithBindings.Should().BeGreaterOrEqualTo(waf1.AggregatedTotalRuntimeWithBindings).And.BeLessOrEqualTo(waf1Elapsed + waf2Elapsed);
        rasp.AggregatedTotalRuntimeWithBindingsRasp.Should().BeGreaterThan(0).And.BeLessOrEqualTo(raspElapsed);
    }

    private static Dictionary<string, object> CreateLargeRequestArgs()
    {
        var body = new List<object>();
        for (var i = 0; i < 256; i++)
        {
            body.Add(new string((char)('a' + (i % 26)), 4096));
        }

        return new Dictionary<string, object>(RequestArgs) { { AddressesConstants.RequestBody, body } };
    }

    private static (IResult Result, ulong ElapsedMicroseconds) Measure(Func<IResult?> run)
    {
        var start = Stopwatch.GetTimestamp();
        var result = run();
        var elapsed = Stopwatch.GetTimestamp() - start;
        result.Should().NotBeNull();

        // rounded up, as Context rounds its totals down
        return (result!, (ulong)Math.Ceiling(StopwatchHelpers.GetElapsedMilliseconds(elapsed) * 1000));
    }

    private void WarmUp()
    {
        // the first runs are slowed down by JIT compilation, which would blur the bounds
        using var context = CreateContext();
        context.Run(CreateLargeRequestArgs(), TimeoutMicroSeconds);
        context.RunWithEphemeral(RaspArgs, TimeoutMicroSeconds, isRasp: true, out _);
    }

    private Context CreateContext()
    {
        var waf = CreateWaf(ruleFile: "rasp-rule-set.json").Waf!;
        return (Context)waf.CreateContext(out _)!;
    }
}
