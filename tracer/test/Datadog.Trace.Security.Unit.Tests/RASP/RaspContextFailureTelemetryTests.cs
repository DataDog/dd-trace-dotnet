// <copyright file="RaspContextFailureTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Moq;
using Xunit;
using AppSecSecurity = Datadog.Trace.AppSec.Security;

namespace Datadog.Trace.Security.Unit.Tests.RASP;

/// <summary>
/// A context that cannot be handed out is classified while _contextSync is held, so the cause cannot be
/// misread by a concurrent disposal. These tests pin the metric each cause produces, and that a RASP run
/// never reports the generic waf.error for it.
/// </summary>
[Collection(nameof(SecuritySequentialTests))]
public class RaspContextFailureTelemetryTests : WafLibraryRequiredTest
{
    [Fact]
    public async Task GivenADisposedAdditiveContext_WhenARaspRunRequestsIt_ThenAnAfterRequestSkipIsReported()
    {
        // the request has ended, so nothing was evaluated: that is a skip, not a binding error
        var waf = CreateWafMock(WafOutcome.Success);
        var requestContext = new AppSecRequestContext();
        requestContext.DisposeAdditiveContext();

        var metrics = await RecordAsync(waf.Object, security => requestContext.GetOrCreateAdditiveContext(security, AddressesConstants.DBStatement));

        var metric = metrics.Should().ContainSingle().Which;
        metric.Name.Should().Be("rasp.rule.skipped");
        metric.Tags.Should().Equal("reason:after-request", "rule_type:sql_injection");

        // one request, one skip: reporting twice would show up as a second point or a count of 2
        metric.Values.Should().Equal(1);
    }

    [Fact]
    public async Task GivenAFailedContextCreation_WhenARaspRunRequestsIt_ThenABindingErrorIsReported()
    {
        var waf = CreateWafMock(WafOutcome.BindingFailed);
        var requestContext = new AppSecRequestContext();

        var metrics = await RecordAsync(waf.Object, security => requestContext.GetOrCreateAdditiveContext(security, AddressesConstants.DBStatement));

        var metric = metrics.Should().ContainSingle().Which;
        metric.Name.Should().Be("rasp.error");
        metric.Tags.Should().Equal("waf_version:unknown", "event_rules_version:unknown", "waf_error:-127", "rule_type:sql_injection");
        metric.Values.Should().Equal(1);
    }

    [Fact]
    public async Task GivenAnUnavailableWaf_WhenARaspRunRequestsAContext_ThenNoMetricIsReported()
    {
        // the WAF was disposed or replaced by a remote configuration update, so nothing was ever handed
        // to it: counting that as a binding error would turn every update into a burst of phantom errors
        var waf = CreateWafMock(WafOutcome.WafUnavailable);
        var requestContext = new AppSecRequestContext();

        var metrics = await RecordAsync(waf.Object, security => requestContext.GetOrCreateAdditiveContext(security, AddressesConstants.DBStatement));

        metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenARealDisposedWaf_WhenARaspRunRequestsAContext_ThenNoMetricIsReported()
    {
        // same cause as above, but reached through the real WAF instead of a mocked outcome: this is the
        // path a remote configuration update or a shutdown actually takes
        var initResult = CreateWaf();
        var waf = initResult.Waf!;
        waf.Dispose();

        var requestContext = new AppSecRequestContext();

        var metrics = await RecordAsync(waf, security => requestContext.GetOrCreateAdditiveContext(security, AddressesConstants.DBStatement));

        // the WAF logs the refusal, which the collector counts under logs_created: what must not appear
        // is a RASP error or skip, nor the generic waf.error a RASP run never reports
        metrics.Should().NotContain(m => m.Name.StartsWith("rasp.") || m.Name == "waf.error");
    }

    [Fact]
    public async Task GivenARealWaf_WhenARaspRunRequestsAContext_ThenNoMetricIsReported()
    {
        // the counterpart of the disposed case: a healthy WAF hands out a context, so nothing is reported
        var initResult = CreateWaf();
        var waf = initResult.Waf!;
        var requestContext = new AppSecRequestContext();
        IContext? context = null;

        var metrics = await RecordAsync(waf, security => context = requestContext.GetOrCreateAdditiveContext(security, AddressesConstants.DBStatement));

        context.Should().NotBeNull();
        metrics.Should().BeEmpty();
        requestContext.DisposeAdditiveContext();
        waf.Dispose();
    }

    [Fact]
    public void GivenASecurityWithoutAWaf_WhenAContextIsRequested_ThenTheWafIsReportedUnavailable()
    {
        // AppSec is enabled but the WAF never initialized, which is a WAF that is gone rather than a
        // failure to hand it anything
        using var security = new AppSecSecurity(waf: null);

        var context = security.CreateAdditiveContext(out var outcome, isRasp: true);

        context.Should().BeNull();
        outcome.Should().Be(WafOutcome.WafUnavailable);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenARaspRun_WhenTheContextIsCreated_ThenTheWafIsToldItServesRasp(bool contextCreated)
    {
        // Waf.CreateContext suppresses the generic waf.error for a RASP run, which only works if the
        // classification actually reaches it
        var waf = CreateWafMock(contextCreated ? WafOutcome.Success : WafOutcome.BindingFailed);
        var requestContext = new AppSecRequestContext();

        await RecordAsync(waf.Object, security => requestContext.GetOrCreateAdditiveContext(security, AddressesConstants.DBStatement));

        waf.Verify(x => x.CreateContext(out It.Ref<WafOutcome>.IsAny, true), Times.Once);
        waf.Verify(x => x.CreateContext(out It.Ref<WafOutcome>.IsAny, false), Times.Never);
    }

    [Fact]
    public async Task GivenANonRaspRun_WhenTheContextIsCreated_ThenTheWafIsNotToldItServesRasp()
    {
        var waf = CreateWafMock(WafOutcome.Success);
        var requestContext = new AppSecRequestContext();

        await RecordAsync(waf.Object, security => requestContext.GetOrCreateAdditiveContext(security));

        waf.Verify(x => x.CreateContext(out It.Ref<WafOutcome>.IsAny, false), Times.Once);
        waf.Verify(x => x.CreateContext(out It.Ref<WafOutcome>.IsAny, true), Times.Never);
    }

    // WafOutcome is internal, so a public theory has to carry it as its underlying value
    [Theory]
    [InlineData((int)WafOutcome.BindingFailed)]
    [InlineData((int)WafOutcome.WafUnavailable)]
    [InlineData((int)WafOutcome.RequestEnded)]
    public async Task GivenANonRaspRun_WhenTheContextIsUnavailable_ThenNoRaspMetricIsReported(int outcomeValue)
    {
        var outcome = (WafOutcome)outcomeValue;
        var waf = CreateWafMock(outcome);
        var requestContext = new AppSecRequestContext();

        if (outcome is WafOutcome.RequestEnded)
        {
            requestContext.DisposeAdditiveContext();
        }

        var metrics = await RecordAsync(waf.Object, security => requestContext.GetOrCreateAdditiveContext(security));

        metrics.Should().NotContain(m => m.Name.StartsWith("rasp."));
    }

    [Fact]
    public async Task GivenARaspRun_WhenTheContextIsCreated_ThenNothingIsReported()
    {
        var waf = CreateWafMock(WafOutcome.Success);
        var requestContext = new AppSecRequestContext();

        var metrics = await RecordAsync(waf.Object, security => requestContext.GetOrCreateAdditiveContext(security, AddressesConstants.DBStatement));

        metrics.Should().BeEmpty();
    }

    private static Mock<IWaf> CreateWafMock(WafOutcome outcome)
    {
        var waf = new Mock<IWaf>();
        waf.SetupGet(x => x.Version).Returns("1.26.0");
        waf.Setup(x => x.IsKnowAddressesSuported()).Returns(true);
        waf.Setup(x => x.GetKnownAddresses()).Returns([AddressesConstants.DBStatement]);

        // Moq assigns the value the out argument held when the setup was recorded, so the cause has to
        // live in a local
        var creationOutcome = outcome;
        waf.Setup(x => x.CreateContext(out creationOutcome, It.IsAny<bool>()))
           .Returns(outcome is WafOutcome.Success ? Mock.Of<IContext>() : null);
        return waf;
    }

    private static async Task<List<(string Name, string[] Tags, int[] Values)>> RecordAsync(IWaf waf, Func<AppSecSecurity, IContext?> run)
    {
        var config = new NameValueCollection
        {
            { ConfigurationKeys.AppSec.Enabled, "1" },
            { ConfigurationKeys.AppSec.RaspEnabled, "1" },
        };

        var settings = new SecuritySettings(new NameValueConfigurationSource(config), NullConfigurationTelemetry.Instance);

        // passing a waf keeps the real init out of the way, but AppsecEnabled is only flipped by that
        // init, so the configuration state has to be built by hand
        var configurationState = new ConfigurationState(settings, NullConfigurationTelemetry.Instance, wafIsNull: false) { AppsecEnabled = true };

        using var security = new AppSecSecurity(settings, waf, rcmSubscriptionManager: Mock.Of<IRcmSubscriptionManager>(), configurationState: configurationState);

        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var previousMetrics = TelemetryFactory.SetMetricsForTesting(collector);

        try
        {
            run(security);
        }
        finally
        {
            TelemetryFactory.SetMetricsForTesting(previousMetrics);
        }

        await collector.DisposeAsync();

        return collector.GetMetrics().Metrics?
                        .Select(m => (m.Metric, m.Tags ?? [], m.Points.Select(p => p.Value).ToArray()))
                        .ToList()
            ?? [];
    }
}
