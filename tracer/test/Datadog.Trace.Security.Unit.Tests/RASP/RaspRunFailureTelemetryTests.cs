// <copyright file="RaspRunFailureTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
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

#if NETCOREAPP
using Datadog.Trace.AppSec.Coordinator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
#endif

namespace Datadog.Trace.Security.Unit.Tests.RASP;

/// <summary>
/// A RASP run that produces no result is classified by the run itself, because the cause is not
/// observable once it has returned: a concurrent disposal would make an ended request, a WAF that is
/// gone and a genuine binding failure look alike. These tests pin the cause each null carries, and the
/// metric it turns into on the real call path.
/// </summary>
[Collection(nameof(SecuritySequentialTests))]
public class RaspRunFailureTelemetryTests : WafLibraryRequiredTest
{
    private static readonly Dictionary<string, object> VulnerableArgs = new() { { AddressesConstants.DBStatement, "SELECT * FROM users WHERE name = 'John' or '1' = '1'" } };

    [Fact]
    public void GivenADisposedContext_WhenAnEphemeralRunHappens_ThenTheRequestIsReportedEnded()
    {
        // the context is disposed when the request it belongs to ends, so a run that gets here arrived
        // too late rather than failed
        var initResult = CreateWaf();
        var waf = initResult.Waf!;
        var context = waf.CreateContext(out _)!;
        context.Dispose();

        var result = context.RunWithEphemeral(VulnerableArgs, TimeoutMicroSeconds, isRasp: true, out var outcome);

        result.Should().BeNull();
        outcome.Should().Be(WafOutcome.RequestEnded);
        waf.Dispose();
    }

    [Fact]
    public void GivenADisposedWaf_WhenAnEphemeralRunHappens_ThenTheWafIsReportedUnavailable()
    {
        // the context outlives a WAF replaced by a remote configuration update, and a run against it
        // never reaches the WAF at all
        var initResult = CreateWaf();
        var waf = initResult.Waf!;
        var context = waf.CreateContext(out _)!;
        waf.Dispose();

        var result = context.RunWithEphemeral(VulnerableArgs, TimeoutMicroSeconds, isRasp: true, out var outcome);

        result.Should().BeNull();
        outcome.Should().Be(WafOutcome.WafUnavailable);
        context.Dispose();
    }

    [Fact]
    public void GivenAnEmptyEphemeralBatch_WhenARunHappens_ThenBindingIsReportedFailed()
    {
        // nothing was handed to the WAF, which is a genuine failure of this run rather than a WAF that
        // is unavailable
        var initResult = CreateWaf();
        var waf = initResult.Waf!;
        var context = waf.CreateContext(out _)!;

        var result = context.RunWithEphemeral(new Dictionary<string, object>(), TimeoutMicroSeconds, isRasp: true, out var outcome);

        result.Should().BeNull();
        outcome.Should().Be(WafOutcome.BindingFailed);
        context.Dispose();
        waf.Dispose();
    }

    [Fact]
    public void GivenAHealthyWaf_WhenAnEphemeralRunHappens_ThenSuccessIsReported()
    {
        // the counterpart of the three failures: a run that produced a result carries its return code,
        // so it must not be classified from the outcome
        var initResult = CreateWaf();
        var waf = initResult.Waf!;
        var context = waf.CreateContext(out _)!;

        var result = context.RunWithEphemeral(VulnerableArgs, TimeoutMicroSeconds, isRasp: true, out var outcome);

        result.Should().NotBeNull();
        outcome.Should().Be(WafOutcome.Success);
        context.Dispose();
        waf.Dispose();
    }

#if NETCOREAPP
    [Fact]
    public async Task GivenADisposedWaf_WhenARaspRunHappens_ThenNoMetricIsReported()
    {
        // the whole point of the typed outcome: a WAF replaced mid-request must not show up as a burst
        // of phantom rasp.error:-127
        var initResult = CreateWaf(ruleFile: "rasp-rule-set.json");
        var waf = initResult.Waf!;
        using var security = new AppSec.Security(waf: waf);
        var coordinator = CreateCoordinator(security);

        // the first run hands out the context, the second one runs against a WAF that is already gone
        coordinator.RunWaf(VulnerableArgs, runWithEphemeral: true, raspAddress: AddressesConstants.DBStatement);
        waf.Dispose();

        var metrics = await RecordAsync(() => coordinator.RunWaf(VulnerableArgs, runWithEphemeral: true, raspAddress: AddressesConstants.DBStatement).Should().BeNull());

        metrics.Should().NotContain(m => m.Name.StartsWith("rasp."));
    }

    [Fact]
    public async Task GivenAnEmptyEphemeralBatch_WhenARaspRunHappens_ThenABindingErrorIsReported()
    {
        var initResult = CreateWaf(ruleFile: "rasp-rule-set.json");
        var waf = initResult.Waf!;
        using var security = new AppSec.Security(waf: waf);
        var coordinator = CreateCoordinator(security);

        var metrics = await RecordAsync(() => coordinator.RunWaf(new Dictionary<string, object>(), runWithEphemeral: true, raspAddress: AddressesConstants.DBStatement).Should().BeNull());

        var metric = metrics.Should().ContainSingle(m => m.Name.StartsWith("rasp.")).Which;
        metric.Name.Should().Be("rasp.error");
        metric.Tags.Should().Contain("waf_error:-127").And.Contain("rule_type:sql_injection");

        // reported once: the run classifies the null, and the RASP guard skips a null result
        metric.Values.Should().Equal(1);
        waf.Dispose();
    }

    [Fact]
    public async Task GivenAMatchingRaspRun_WhenItSucceeds_ThenNoErrorIsReported()
    {
        var initResult = CreateWaf(ruleFile: "rasp-rule-set.json");
        var waf = initResult.Waf!;
        using var security = new AppSec.Security(waf: waf);
        var coordinator = CreateCoordinator(security);

        var metrics = await RecordAsync(() => coordinator.RunWaf(VulnerableArgs, runWithEphemeral: true, raspAddress: AddressesConstants.DBStatement).Should().NotBeNull());

        metrics.Should().NotContain(m => m.Name == "rasp.error");
        metrics.Should().NotContain(m => m.Name == "rasp.rule.skipped");
        waf.Dispose();
    }

    private static SecurityCoordinator CreateCoordinator(AppSec.Security security)
    {
        var httpContextMock = new Mock<HttpContext>();
        var httpResponseMock = new Mock<HttpResponse>();
        httpResponseMock.Setup(x => x.StatusCode).Returns(200);
        httpContextMock.Setup(x => x.Response).Returns(httpResponseMock.Object);
        var routingFeature = new Mock<IRoutingFeature>();
        routingFeature.Setup(x => x.RouteData).Returns(new RouteData());
        httpContextMock.Setup(x => x.Features[typeof(IRoutingFeature)]).Returns(routingFeature.Object);

        var traceContext = new TraceContext(new EmptyDatadogTracer());
        var spanContext = new SpanContext(parent: null, traceContext, serviceName: "test", traceId: (TraceId)100, spanId: 200);
        var span = new Span(spanContext, DateTimeOffset.Now);

        return SecurityCoordinator.Get(security, span, new SecurityCoordinator.HttpTransport(httpContextMock.Object));
    }
#endif

    private static async Task<List<(string Name, string[] Tags, int[] Values)>> RecordAsync(Action run)
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var previousMetrics = TelemetryFactory.SetMetricsForTesting(collector);

        try
        {
            run();
        }
        finally
        {
            TelemetryFactory.SetMetricsForTesting(previousMetrics);
        }

        await collector.DisposeAsync();

        // the point values are what make a single report distinguishable from a duplicated one: two
        // increments of the same series aggregate into one point of value 2
        return collector.GetMetrics().Metrics?
                        .Select(m => (m.Metric, m.Tags ?? [], m.Points.Select(p => p.Value).ToArray()))
                        .ToList()
            ?? [];
    }
}
