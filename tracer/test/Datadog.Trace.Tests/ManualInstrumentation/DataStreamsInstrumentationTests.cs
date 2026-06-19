// <copyright file="DataStreamsInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.DataStreams;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ManualInstrumentation;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class DataStreamsInstrumentationTests
{
    [Fact]
    public async Task ActiveSpan_SetsTagOnSpan()
    {
        var settings = TracerSettings.Create(
            new()
            {
                { ConfigurationKeys.DataStreamsMonitoring.Enabled, "true" },
                { ConfigurationKeys.Environment, "foo" },
                { ConfigurationKeys.ServiceName, "bar" },
                { ConfigurationKeys.PropagateProcessTags, "false" },
            });
        var scopeManager = new AsyncLocalScopeManager();
        await using var tracer = TracerHelper.Create(settings, scopeManager: scopeManager);
        TracerRestorerAttribute.SetTracer(tracer);

        var span = new Span(new SpanContext(traceId: 1, spanId: 1), DateTimeOffset.UtcNow);
        using var scope = scopeManager.Activate(span, finishOnClose: false);

        DataStreamsTrackTransactionIntegration.OnMethodBegin<object>("tx-123", "my-checkpoint");

        span.GetTag("dsm.transaction.id").Should().Be("tx-123");
    }

    [Fact]
    public async Task NoActiveSpan_DoesNotThrow()
    {
        var settings = TracerSettings.Create(
            new()
            {
                { ConfigurationKeys.DataStreamsMonitoring.Enabled, "true" },
                { ConfigurationKeys.Environment, "foo" },
                { ConfigurationKeys.ServiceName, "bar" },
                { ConfigurationKeys.PropagateProcessTags, "false" },
            });
        var scopeManager = new AsyncLocalScopeManager();
        await using var tracer = TracerHelper.Create(settings, scopeManager: scopeManager);
        TracerRestorerAttribute.SetTracer(tracer);

        var act = () => DataStreamsTrackTransactionIntegration.OnMethodBegin<object>("tx-789", "no-span-checkpoint");

        act.Should().NotThrow();
    }
}
