// <copyright file="OtlpSecurityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.OpenTelemetry.Common;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Moq;
using Xunit;
using AppSecSecurity = Datadog.Trace.AppSec.Security;
using IastRuntime = Datadog.Trace.Iast.Iast;

namespace Datadog.Trace.Tests.OpenTelemetry;

[Collection(nameof(TracerInstanceTestCollection))]
public class OtlpSecurityTests : IAsyncLifetime
{
    private readonly Tracer _previousTracer = Tracer.Instance;
    private readonly ScopedTracer _tracer;
    private readonly AppSecSecurity _previousSecurity = AppSecSecurity.Instance;
    private readonly IastRuntime _previousIast = IastRuntime.Instance;
    private readonly ConfigurationState _configurationState;
    private readonly AppSecSecurity _security;

    public OtlpSecurityTests()
    {
        _tracer = TracerHelper.CreateWithFakeAgent(new TracerSettings(NullConfigurationSource.Instance));
        Tracer.UnsafeSetTracerInstance(_tracer);
        var settings = new SecuritySettings(new DictionaryConfigurationSource(new Dictionary<string, string> { [ConfigurationKeys.AppSec.Enabled] = "0" }), NullConfigurationTelemetry.Instance);
        _configurationState = new ConfigurationState(settings, NullConfigurationTelemetry.Instance, wafIsNull: true);
        _security = new AppSecSecurity(settings, rcmSubscriptionManager: Mock.Of<IRcmSubscriptionManager>(), configurationState: _configurationState);
        AppSecSecurity.Instance = _security;
        IastRuntime.Instance = new IastRuntime(new IastSettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance), NullDiscoveryService.Instance);
    }

    [Theory]
    [CombinatorialData]
    public void ReportProducersPreserveJsonFallbackWithoutAgentCapabilities(bool otlp)
    {
        var span = CreateSpan(otlp: otlp, apmEnabled: true);
        var appsec = new AppSecRequestContext();
        appsec.AddWafSecurityEvents([new Dictionary<string, object> { ["rule"] = new Dictionary<string, object> { ["id"] = "test-rule" } }]);
        appsec.CloseWebSpan(span);
        var iast = new IastRequestContext();
        iast.AddVulnerability(new Vulnerability("WEAK_HASH", new Location("Test.Hash"), new Evidence("MD5")));
        iast.AddIastVulnerabilitiesToSpan(span);

        span.GetTag(Tags.AppSecJson).Should().Contain("test-rule");
        span.GetTag(Tags.IastJson).Should().Contain("WEAK_HASH");
        var attributes = GetAttributes(span);
        attributes.FindAll(kv => kv.Key == "appsec" && kv.Value is byte[]).Should().HaveCount(otlp ? 1 : 0);
        attributes.FindAll(kv => kv.Key == "iast" && kv.Value is byte[]).Should().HaveCount(otlp ? 1 : 0);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void SecurityMetadataMatchesNativePlacement(bool enabled, bool wafExecuted, bool localRoot)
    {
        _configurationState.AppsecEnabled = enabled;
        var root = CreateSpan(otlp: true, apmEnabled: false);
        root.Context.TraceContext.Should().BeOfType<TraceContext>().Which.WafExecuted = wafExecuted;
        var span = localRoot ? root : new Span(new SpanContext(root.Context, root.Context.TraceContext, "service"), DateTimeOffset.UtcNow);
        var attributes = GetAttributes(span);

        attributes.Should().Contain(kv => kv.Key == Trace.Metrics.ApmEnabled && Equals(kv.Value, 0.0));
        if (enabled && wafExecuted && localRoot)
        {
            attributes.Should().Contain(kv => kv.Key == Trace.Metrics.AppSecEnabled && Equals(kv.Value, 1.0));
            attributes.Should().Contain(kv => kv.Key == Tags.RuntimeFamily && Equals(kv.Value, "dotnet"));
            attributes.Should().Contain(kv => kv.Key == Tags.AppSecRuleFileVersion && Equals(kv.Value, _security.WafRuleFileVersion));
        }
        else
        {
            attributes.Should().NotContain(kv => kv.Key == Trace.Metrics.AppSecEnabled || kv.Key == Tags.RuntimeFamily || kv.Key == Tags.AppSecRuleFileVersion);
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        Tracer.UnsafeSetTracerInstance(_previousTracer);
        AppSecSecurity.Instance = _previousSecurity;
        IastRuntime.Instance = _previousIast;
        _security.Dispose();
        await _tracer.DisposeAsync();
    }

    private static Span CreateSpan(bool otlp, bool apmEnabled)
    {
        var settings = new TracerSettings(new DictionaryConfigurationSource(new Dictionary<string, string>
        {
            ["OTEL_TRACES_EXPORTER"] = otlp ? "otlp" : "datadog",
            ["OTEL_EXPORTER_OTLP_TRACES_PROTOCOL"] = "http/json",
            [ConfigurationKeys.ApmTracingEnabled] = apmEnabled.ToString(),
        }));
        return new Span(new SpanContext(parent: null, new TraceContext(new StubDatadogTracer(settings)), "service"), DateTimeOffset.UtcNow);
    }

    private static List<KeyValue> GetAttributes(Span span)
    {
        var attributes = new List<KeyValue>();
        var chunk = new TraceChunkModel(new SpanCollection([span]));
        OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), chunk.GetSpanModel(0), limit: 128);
        return attributes;
    }
}
