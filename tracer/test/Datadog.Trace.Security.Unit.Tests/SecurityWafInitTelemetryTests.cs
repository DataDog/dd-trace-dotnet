// <copyright file="SecurityWafInitTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Collectors;
using FluentAssertions;
using Moq;
using Xunit;
using AppSecSecurity = Datadog.Trace.AppSec.Security;

namespace Datadog.Trace.Security.Unit.Tests;

[Collection(nameof(SecuritySequentialTests))]
public class SecurityWafInitTelemetryTests : WafLibraryRequiredTest
{
    [Fact]
    public async Task GivenAWafInitFailure_WhenReported_ThenTheLibraryVersionIsTagged()
    {
        var expectedVersion = WafLibraryInvoker!.GetVersion();
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var previousMetrics = TelemetryFactory.SetMetricsForTesting(collector);

        try
        {
            using var security = CreateSecurityWithUnusableRules();
            security.InitializationError.Should().Be("Error initializing waf");
        }
        finally
        {
            TelemetryFactory.SetMetricsForTesting(previousMetrics);
        }

        await collector.DisposeAsync();

        var wafInit = collector.GetMetrics().Metrics!.Should().ContainSingle(m => m.Metric == "waf.init").Which;
        wafInit.Tags.Should().Contain("success:false").And.Contain($"waf_version:{expectedVersion}");
    }

    [Fact]
    public void GivenAWafInitFailure_WhenTheVersionIsQueried_ThenTheLibraryVersionIsReturned()
    {
        // the tracer manager re-applies this value to every waf metric once it starts
        using var security = CreateSecurityWithUnusableRules();

        security.DdlibWafVersion.Should().Be(WafLibraryInvoker!.GetVersion());
    }

    private AppSecSecurity CreateSecurityWithUnusableRules()
    {
        var source = CreateConfigurationSource((ConfigurationKeys.AppSec.Enabled, "1"), (ConfigurationKeys.AppSec.Rules, "unexisting-rule-set.json"));
        var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);
        return new AppSecSecurity(settings, rcmSubscriptionManager: Mock.Of<IRcmSubscriptionManager>());
    }
}
