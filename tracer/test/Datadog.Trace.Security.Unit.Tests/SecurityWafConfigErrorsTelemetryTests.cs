// <copyright file="SecurityWafConfigErrorsTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Collectors;
using FluentAssertions;
using Moq;
using Xunit;
using Action = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;
using AppSecSecurity = Datadog.Trace.AppSec.Security;

namespace Datadog.Trace.Security.Unit.Tests;

public class SecurityWafConfigErrorsTelemetryTests : WafLibraryRequiredTest
{
    private const string ConfigErrorsMetric = "waf.config_errors";

    [Fact]
    public async Task GivenAnUpdateWithDuplicateActionIds_WhenApplied_ThenAnUpdateConfigErrorIsReported()
    {
        var configurationState = CreateConfigurationState(null);
        using var waf = CreateWaf(configurationState).Waf!;

        var metrics = await RecordMetricsAsync(() =>
        {
            AddAsmRemoteConfig(configurationState, new Payload { Actions = [BlockAction(405), BlockAction(505)] }, "duplicate-actions");
            UpdateWaf(configurationState, waf);
        });

        var configErrors = metrics.Should().ContainSingle(m => m.Metric == ConfigErrorsMetric).Which;
        configErrors.Tags.Should().BeEquivalentTo("waf_version:unknown", "event_rules_version:unknown", "action:update");
        configErrors.Points.Sum(p => p.Value).Should().Be(1);
    }

    [Fact]
    public async Task GivenAValidUpdate_WhenApplied_ThenNoConfigErrorIsReported()
    {
        var configurationState = CreateConfigurationState(null);
        using var waf = CreateWaf(configurationState).Waf!;

        var metrics = await RecordMetricsAsync(() =>
        {
            AddAsmRemoteConfig(configurationState, new Payload { Actions = [BlockAction(405)] }, "valid-actions");
            UpdateWaf(configurationState, waf).Success.Should().BeTrue();
        });

        metrics.Should().NotContain(m => m.Metric == ConfigErrorsMetric);
    }

    [Fact]
    public async Task GivenAnUpdateWithAnInvalidAndAValidConfig_WhenApplied_ThenTheInvalidConfigErrorIsReported()
    {
        // the diagnostics of every config must be counted, not only those of the last one applied
        var configurationState = CreateConfigurationState(null);
        using var waf = CreateWaf(configurationState).Waf!;

        var metrics = await RecordMetricsAsync(() =>
        {
            AddAsmRemoteConfig(configurationState, new Payload { Actions = [BlockAction(405), BlockAction(505)] }, "a-duplicate-actions");
            AddAsmRemoteConfig(configurationState, new Payload { Actions = [new Action { Id = "other", Type = "block_request", Parameters = new Parameter { StatusCode = 403, Type = "auto" } }] }, "z-valid-actions");
            UpdateWaf(configurationState, waf);
        });

        var configErrors = metrics.Should().ContainSingle(m => m.Metric == ConfigErrorsMetric).Which;
        configErrors.Tags.Should().Contain("action:update");
        configErrors.Points.Sum(p => p.Value).Should().Be(1);
    }

    [Fact]
    public async Task GivenACorruptedRulesFile_WhenTheWafFailsToInit_ThenAnInitConfigErrorIsReported()
    {
        var rulesFile = Path.GetTempFileName();
        File.WriteAllText(rulesFile, """{"corrupted": "data"}""");

        try
        {
            var metrics = await RecordMetricsAsync(() =>
            {
                using var security = CreateSecurity(rulesFile);
                security.InitializationError.Should().Be("Error initializing waf");
            });

            var configErrors = metrics.Should().ContainSingle(m => m.Metric == ConfigErrorsMetric).Which;
            configErrors.Tags.Should().BeEquivalentTo($"waf_version:{WafLibraryInvoker!.GetVersion()}", "event_rules_version:unknown", "action:init");
            configErrors.Points.Sum(p => p.Value).Should().Be(1);
        }
        finally
        {
            File.Delete(rulesFile);
        }
    }

    [Fact]
    public async Task GivenARulesetWhoseOnlyRuleIsInvalid_WhenTheWafFailsToInit_ThenOnlyTheReportedErrorsAreCounted()
    {
        // the waf reports the invalid rule, so the failed init must not add an error of its own
        var rulesFile = Path.GetTempFileName();
        File.WriteAllText(rulesFile, """{"version": "2.2", "metadata": {"rules_version": "1.10.0"}, "rules": [{"enabled": true, "tags": {"module": "rasp"}}]}""");

        try
        {
            var metrics = await RecordMetricsAsync(() =>
            {
                using var security = CreateSecurity(rulesFile);
                security.InitializationError.Should().Be("Error initializing waf");
            });

            var configErrors = metrics.Should().ContainSingle(m => m.Metric == ConfigErrorsMetric).Which;
            configErrors.Tags.Should().Contain("action:init");
            configErrors.Points.Sum(p => p.Value).Should().Be(1);
        }
        finally
        {
            File.Delete(rulesFile);
        }
    }

    [Fact]
    public async Task GivenTheDefaultRules_WhenTheWafInits_ThenNoConfigErrorIsReported()
    {
        var metrics = await RecordMetricsAsync(() =>
        {
            using var security = CreateSecurity(rulesFile: null);
            security.InitializationError.Should().BeNull();
        });

        metrics.Should().NotContain(m => m.Metric == ConfigErrorsMetric);
    }

    private static Action BlockAction(int statusCode) => new() { Id = "block", Type = "block_request", Parameters = new Parameter { StatusCode = statusCode, Type = "html" } };

    private static async Task<List<MetricData>> RecordMetricsAsync(System.Action run)
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

        return collector.GetMetrics().Metrics?.ToList() ?? [];
    }

    private AppSecSecurity CreateSecurity(string? rulesFile)
    {
        var source = CreateConfigurationSource((ConfigurationKeys.AppSec.Enabled, "1"), (ConfigurationKeys.AppSec.Rules, rulesFile));
        var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);
        return new AppSecSecurity(settings, rcmSubscriptionManager: Mock.Of<IRcmSubscriptionManager>());
    }
}
