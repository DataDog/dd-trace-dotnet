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
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Moq;
using Xunit;
using AppSecSecurity = Datadog.Trace.AppSec.Security;

namespace Datadog.Trace.Security.Unit.Tests.RASP;

/// <summary>
/// An unavailable WAF context is classified while _contextSync is held, so the cause cannot be misread
/// by a concurrent disposal. These tests pin the metric each cause produces, and that a RASP run never
/// reports the generic waf.error for it.
/// </summary>
[Collection(nameof(SecuritySequentialTests))]
public class RaspContextFailureTelemetryTests
{
    [Fact]
    public async Task GivenADisposedAdditiveContext_WhenARaspRunRequestsIt_ThenAnAfterRequestSkipIsReported()
    {
        // the request has ended, so nothing was evaluated: that is a skip, not a binding error
        var waf = CreateWaf(contextCreated: true);
        var requestContext = new AppSecRequestContext();
        requestContext.DisposeAdditiveContext();

        var metrics = await RecordAsync(waf, security => requestContext.GetOrCreateAdditiveContext(security, AddressesConstants.DBStatement));

        var metric = metrics.Should().ContainSingle().Which;
        metric.Name.Should().Be("rasp.rule.skipped");
        metric.Tags.Should().Equal("reason:after-request", "rule_type:sql_injection");

        // one request, one skip: reporting twice would show up as a second point or a count of 2
        metric.Values.Should().Equal(1);
    }

    [Fact]
    public async Task GivenAFailedContextCreation_WhenARaspRunRequestsIt_ThenABindingErrorIsReported()
    {
        var waf = CreateWaf(contextCreated: false);
        var requestContext = new AppSecRequestContext();

        var metrics = await RecordAsync(waf, security => requestContext.GetOrCreateAdditiveContext(security, AddressesConstants.DBStatement));

        var metric = metrics.Should().ContainSingle().Which;
        metric.Name.Should().Be("rasp.error");
        metric.Tags.Should().Equal("waf_version:unknown", "event_rules_version:unknown", "waf_error:-127", "rule_type:sql_injection");
        metric.Values.Should().Equal(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenARaspRun_WhenTheContextIsCreated_ThenTheWafIsToldItServesRasp(bool contextCreated)
    {
        // Waf.CreateContext suppresses the generic waf.error for a RASP run, which only works if the
        // classification actually reaches it
        var waf = CreateWaf(contextCreated);
        var requestContext = new AppSecRequestContext();

        await RecordAsync(waf, security => requestContext.GetOrCreateAdditiveContext(security, AddressesConstants.DBStatement));

        waf.Verify(x => x.CreateContext(true), Times.Once);
        waf.Verify(x => x.CreateContext(false), Times.Never);
    }

    [Fact]
    public async Task GivenANonRaspRun_WhenTheContextIsCreated_ThenTheWafIsNotToldItServesRasp()
    {
        var waf = CreateWaf(contextCreated: true);
        var requestContext = new AppSecRequestContext();

        await RecordAsync(waf, security => requestContext.GetOrCreateAdditiveContext(security));

        waf.Verify(x => x.CreateContext(false), Times.Once);
        waf.Verify(x => x.CreateContext(true), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenANonRaspRun_WhenTheContextIsUnavailable_ThenNoRaspMetricIsReported(bool disposed)
    {
        var waf = CreateWaf(contextCreated: false);
        var requestContext = new AppSecRequestContext();

        if (disposed)
        {
            requestContext.DisposeAdditiveContext();
        }

        var metrics = await RecordAsync(waf, security => requestContext.GetOrCreateAdditiveContext(security));

        metrics.Should().NotContain(m => m.Name.StartsWith("rasp."));
    }

    [Fact]
    public async Task GivenARaspRun_WhenTheContextIsCreated_ThenNothingIsReported()
    {
        var waf = CreateWaf(contextCreated: true);
        var requestContext = new AppSecRequestContext();

        var metrics = await RecordAsync(waf, security => requestContext.GetOrCreateAdditiveContext(security, AddressesConstants.DBStatement));

        metrics.Should().BeEmpty();
    }

    private static Mock<IWaf> CreateWaf(bool contextCreated)
    {
        var waf = new Mock<IWaf>();
        waf.SetupGet(x => x.Version).Returns("1.26.0");
        waf.Setup(x => x.IsKnowAddressesSuported()).Returns(true);
        waf.Setup(x => x.GetKnownAddresses()).Returns([AddressesConstants.DBStatement]);
        waf.Setup(x => x.CreateContext(It.IsAny<bool>())).Returns(contextCreated ? Mock.Of<IContext>() : null);
        return waf;
    }

    private static async Task<List<(string Name, string[] Tags, int[] Values)>> RecordAsync(Mock<IWaf> waf, Func<AppSecSecurity, IContext?> run)
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

        using var security = new AppSecSecurity(settings, waf.Object, rcmSubscriptionManager: Mock.Of<IRcmSubscriptionManager>(), configurationState: configurationState);

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
