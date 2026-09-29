// <copyright file="SecurityWafInitTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    // a versioned ruleset whose only rule is invalid, so the WAF reports the version but cannot be built
    private const string VersionedRulesetWithoutUsableRules = """{"version": "2.2", "metadata": {"rules_version": "1.10.0"}, "rules": [{"enabled": true, "tags": {"module": "rasp"}}]}""";

    [Fact]
    public async Task GivenAWafInitFailure_WhenReported_ThenTheLibraryVersionIsTagged()
    {
        string? initializationError = null;
        var wafInit = await RecordWafInitAsync(() =>
        {
            using var security = CreateSecurity("unexisting-rule-set.json", enabled: true);
            initializationError = security.InitializationError;
        });

        initializationError.Should().Be("Error initializing waf");
        wafInit.Tags.Should().BeEquivalentTo($"waf_version:{WafLibraryInvoker!.GetVersion()}", "event_rules_version:unknown", "success:false");
        wafInit.Points.Select(p => p.Value).Should().Equal(1);
    }

    [Fact]
    public async Task GivenAWafInitFailure_WhenTheVersionIsQueried_ThenTheLibraryVersionIsReturned()
    {
        // the tracer manager re-applies this value to every waf metric once it starts
        string? version = null;
        await RecordWafInitAsync(() =>
        {
            using var security = CreateSecurity("unexisting-rule-set.json", enabled: true);
            version = security.DdlibWafVersion;
        });

        version.Should().Be(WafLibraryInvoker!.GetVersion());
    }

    [Fact]
    public async Task GivenARemoteEnableWithAVersionedButUnusableRuleset_WhenTheWafFailsToInit_ThenTheInitErrorAndLibraryVersionAreReported()
    {
        var rulesFile = Path.GetTempFileName();
        File.WriteAllText(rulesFile, VersionedRulesetWithoutUsableRules);

        try
        {
            ApplyDetails[]? applyDetails = null;
            string? initError = null;
            var wafInit = await RecordWafInitAsync(() =>
            {
                using var security = CreateSecurity(rulesFile, enabled: false);
                applyDetails = EnableFromRcm(security);
                initError = security.WafInitResult?.ErrorMessage;
            });

            initError.Should().NotBeNullOrEmpty();
            applyDetails.Should().ContainSingle().Which.Error.Should().Be(initError);
            wafInit.Tags.Should().BeEquivalentTo($"waf_version:{WafLibraryInvoker!.GetVersion()}", "event_rules_version:1.10.0", "success:false");
            wafInit.Points.Select(p => p.Value).Should().Equal(1);
        }
        finally
        {
            File.Delete(rulesFile);
        }
    }

    private static async Task<MetricData> RecordWafInitAsync(Action run)
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

        return collector.GetMetrics().Metrics!.Should().ContainSingle(m => m.Metric == "waf.init").Which;
    }

    private static ApplyDetails[] EnableFromRcm(AppSecSecurity security)
    {
        var content = Encoding.UTF8.GetBytes("""{"asm": {"enabled": true}}""");
        var config = new RemoteConfiguration(RemoteConfigurationPath.FromPath("datadog/2/ASM_FEATURES/asm_features_activation/config"), content, content.Length, new Dictionary<string, string>(), 1);
        var configs = new Dictionary<string, List<RemoteConfiguration>> { [RcmProducts.AsmFeatures] = [config] };
        var method = typeof(AppSecSecurity).GetMethod("UpdateFromRcm", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (ApplyDetails[])method.Invoke(security, [configs, null])!;
    }

    private AppSecSecurity CreateSecurity(string rulesFile, bool enabled)
    {
        // leaving AppSec unset rather than disabled is what lets remote config enable it
        var source = enabled
                         ? CreateConfigurationSource((ConfigurationKeys.AppSec.Enabled, "1"), (ConfigurationKeys.AppSec.Rules, rulesFile))
                         : CreateConfigurationSource((ConfigurationKeys.AppSec.Rules, rulesFile));
        var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);
        return new AppSecSecurity(settings, rcmSubscriptionManager: Mock.Of<IRcmSubscriptionManager>());
    }
}
