// <copyright file="DataStreamsInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias DatadogTraceManual;

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.DataStreams;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Aggregation;
using Datadog.Trace.DataStreamsMonitoring.TransactionTracking;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;
using ManualISpan = DatadogTraceManual::Datadog.Trace.ISpan;

namespace Datadog.Trace.Tests.ManualInstrumentation;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class DataStreamsInstrumentationTests
{
    [Fact]
    public void HappyPath_DuckTypedRealSpan_SetsTagAndTracksTransaction()
    {
        var dsm = CreateDsmWithWriter(out var writer);
        var manager = CreateTracerManager(dsm);
        TracerRestorerAttribute.SetTracer(Tracer.Instance, manager);

        var span = new Span(new SpanContext(traceId: 1, spanId: 1), DateTimeOffset.UtcNow);
        var manualSpan = span.DuckCast<ManualISpan>();

        DataStreamsTrackTransactionIntegration.OnMethodBegin<object, ManualISpan>(ref manualSpan, "tx-123", "my-checkpoint");

        span.GetTag("dsm.transaction.id").Should().Be("tx-123");
        writer.TransactionCount.Should().Be(1);
    }

    [Fact]
    public void CustomISpan_MockSpan_SkipsTagButTracksTransaction()
    {
        var dsm = CreateDsmWithWriter(out var writer);
        var manager = CreateTracerManager(dsm);
        TracerRestorerAttribute.SetTracer(Tracer.Instance, manager);

        var mockSpanObject = new Mock<ManualISpan>();
        var manualSpan = mockSpanObject.Object;

        DataStreamsTrackTransactionIntegration.OnMethodBegin<object, ManualISpan>(ref manualSpan, "tx-789", "custom-checkpoint");

        mockSpanObject.Verify(s => s.SetTag(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        writer.TransactionCount.Should().Be(1);
    }

    private static DataStreamsManager CreateDsmWithWriter(out DataStreamsWriterStub writer)
    {
        writer = new DataStreamsWriterStub();
        var settings = TracerSettings.Create(
            new()
            {
                { ConfigurationKeys.Environment, "foo" },
                { ConfigurationKeys.ServiceName, "bar" },
                { ConfigurationKeys.DataStreamsMonitoring.Enabled, "true" },
                { ConfigurationKeys.PropagateProcessTags, "false" },
            });
        return new DataStreamsManager(settings, writer, Mock.Of<IDiscoveryService>());
    }

    private static TracerManager CreateTracerManager(DataStreamsManager dsm)
    {
        return new TracerManager(
            settings: new TracerSettings(),
            agentWriter: null,
            scopeManager: null,
            statsd: null,
            runtimeMetricsWriter: null,
            directLogSubmission: null,
            telemetry: null,
            discoveryService: null,
            dataStreamsManager: dsm,
            gitMetadataTagsProvider: null,
            traceSampler: null,
            spanSampler: null,
            remoteConfigurationManager: Mock.Of<IRemoteConfigurationManager>(),
            dynamicConfigurationManager: Mock.Of<IDynamicConfigurationManager>(),
            tracerFlareManager: Mock.Of<ITracerFlareManager>(),
            spanEventsManager: Mock.Of<ISpanEventsManager>(),
            featureFlagsModule: null,
            serviceRemappingHash: null);
    }

    internal class DataStreamsWriterStub : IDataStreamsWriter
    {
        private int _transactionCount;

        public int TransactionCount => Volatile.Read(ref _transactionCount);

        public void Add(in StatsPoint point)
        {
        }

        public void AddBacklog(in BacklogPoint point)
        {
        }

        public void AddTransaction(in DataStreamsTransactionInfo transaction)
        {
            Interlocked.Increment(ref _transactionCount);
        }

        public Task FlushAsync()
        {
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
