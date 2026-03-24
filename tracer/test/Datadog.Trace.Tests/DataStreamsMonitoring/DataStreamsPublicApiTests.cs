// <copyright file="DataStreamsPublicApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.DataStreams;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsPublicApiTests
{
    /// <summary>
    /// Verifies the full wiring: integration sets the span tag when DSM is enabled.
    /// The tag is the observable side-effect accessible without a mock writer.
    /// </summary>
    [Fact]
    public async Task Invoke_WhenDsmEnabled_SetsSpanTag()
    {
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.DataStreamsMonitoring.Enabled, true } });
        await using var tracer = TracerHelper.Create(settings);
        var span = new Span(new SpanContext(traceId: 123, spanId: 456), DateTimeOffset.UtcNow);

        DataStreamsTrackTransactionIntegration.Invoke(tracer, span, "tx-abc", "my-checkpoint");

        span.Tags.GetTag("dsm.transaction.id").Should().Be("tx-abc");
    }

    [Fact]
    public async Task Invoke_WhenDsmDisabled_DoesNotSetTag()
    {
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.DataStreamsMonitoring.Enabled, false } });
        await using var tracer = TracerHelper.Create(settings);
        var span = new Span(new SpanContext(traceId: 123, spanId: 456), DateTimeOffset.UtcNow);

        DataStreamsTrackTransactionIntegration.Invoke(tracer, span, "tx-abc", "my-checkpoint");

        span.Tags.GetTag("dsm.transaction.id").Should().BeNull();
    }

    [Fact]
    public async Task Invoke_WithNullSpan_DoesNotThrow()
    {
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.DataStreamsMonitoring.Enabled, true } });
        await using var tracer = TracerHelper.Create(settings);

        var act = () => DataStreamsTrackTransactionIntegration.Invoke(tracer, (Span)null, "tx-abc", "my-checkpoint");
        act.Should().NotThrow();
    }
}
