// <copyright file="OtlpSecurityMetadataTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry;

[Collection(nameof(TracerInstanceTestCollection))]
public class OtlpSecurityMetadataTests
{
    [Theory]
    [InlineData(true, true, true, "1.2.3")]
    [InlineData(true, true, true, null)]
    [InlineData(false, true, true, "1.2.3")]
    [InlineData(true, false, true, "1.2.3")]
    [InlineData(true, true, false, "1.2.3")]
    public void EmitsWafMetadataOnlyOnQualifyingRoot(bool enabled, bool wafRan, bool localRoot, string? ruleVersion)
    {
        var settings = new SecuritySettings(null, NullConfigurationTelemetry.Instance);
        var state = new ConfigurationState(settings, NullConfigurationTelemetry.Instance, wafIsNull: true);
        using var security = new Security(settings, rcmSubscriptionManager: Mock.Of<IRcmSubscriptionManager>(), configurationState: state) { WafRuleFileVersion = ruleVersion };
        state.AppsecEnabled = enabled;
        var tracer = new StubDatadogTracer(TracerSettings.Create(new() { { ConfigurationKeys.ApmTracingEnabled, false } }));
        var trace = new TraceContext(tracer) { WafExecuted = wafRan };
        var parent = localRoot ? null : new SpanContext(null, trace, "service");
        var span = new Span(new SpanContext(parent, trace, "service"), DateTimeOffset.UtcNow);
        span.SetMetric(Trace.Metrics.ApmEnabled, 1d);
        var model = new TraceChunkModel(new SpanCollection([span])).GetSpanModel(0);
        var attributes = new Dictionary<string, object?>();
        var previous = Security.Instance;
        try
        {
            Security.Instance = security;
            OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv.Key, kv.Value), in model, limit: 128);
        }
        finally
        {
            Security.Instance = previous;
        }

        attributes[Trace.Metrics.ApmEnabled].Should().Be(0d);
        var qualifies = enabled && wafRan && localRoot;
        if (qualifies)
        {
            attributes[Trace.Metrics.AppSecEnabled].Should().Be(1d);
            attributes[Tags.RuntimeFamily].Should().Be("dotnet");
        }
        else
        {
            attributes.Should().NotContainKey(Trace.Metrics.AppSecEnabled).And.NotContainKey(Tags.RuntimeFamily);
        }

        attributes.TryGetValue(Tags.AppSecRuleFileVersion, out var version).Should().Be(qualifies && ruleVersion is not null);
        version.Should().Be(qualifies ? ruleVersion : null);
    }
}
