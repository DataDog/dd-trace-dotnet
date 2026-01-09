// <copyright file="TestOptimizationTracerManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Ci
{
    internal class TestOptimizationTracerManager : TracerManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TestOptimizationTracerManager>();

        public TestOptimizationTracerManager(
            TracerSettings settings,
            IAgentWriter agentWriter,
            IScopeManager scopeManager,
            IStatsdManager statsd,
            RuntimeMetricsWriter runtimeMetricsWriter,
            DirectLogSubmissionManager logSubmissionManager,
            ITelemetryController telemetry,
            IDiscoveryService discoveryService,
            DataStreamsManager dataStreamsManager,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            ITraceSampler traceSampler,
            ISpanSampler spanSampler,
            IRemoteConfigurationManager remoteConfigurationManager,
            IDynamicConfigurationManager dynamicConfigurationManager,
            ITracerFlareManager tracerFlareManager,
            ISpanEventsManager spanEventsManager)
            : base(
                settings,
                agentWriter,
                scopeManager,
                statsd,
                runtimeMetricsWriter,
                logSubmissionManager,
                telemetry,
                discoveryService,
                dataStreamsManager,
                gitMetadataTagsProvider,
                traceSampler,
                spanSampler,
                remoteConfigurationManager,
                dynamicConfigurationManager,
                tracerFlareManager,
                spanEventsManager,
                GetProcessors(settings.PartialFlushEnabled, agentWriter is CIVisibilityProtocolWriter))
        {
        }

        private static Trace.Processors.ITraceProcessor[] GetProcessors(bool partialFlushEnabled, bool isCiVisibilityProtocol)
        {
            if (isCiVisibilityProtocol)
            {
                return
                [
                    new Trace.Processors.NormalizerTraceProcessor(),
                    new Trace.Processors.TruncatorTraceProcessor(),
                    new Processors.OriginTagTraceProcessor(partialFlushEnabled, true)
                ];
            }

            return
            [
                new Trace.Processors.NormalizerTraceProcessor(),
                new Trace.Processors.TruncatorTraceProcessor(),
                new Processors.TestSuiteVisibilityProcessor(),
                new Processors.OriginTagTraceProcessor(partialFlushEnabled, false)
            ];
        }

        private Span? ProcessSpan(Span span)
        {
            if (span is null)
            {
                return span;
            }

            foreach (var processor in TraceProcessors)
            {
                if (processor is null)
                {
                    continue;
                }

                try
                {
                    if (processor.Process(span) is { } nSpan)
                    {
                        span = nSpan;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error executing trace processor {TraceProcessorType}", processor?.GetType());
                }
            }

            return span;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEvent(IEvent @event)
        {
            if (@event is TestEvent { Content: { } test } testEvent)
            {
                if (ProcessSpan(test) is { } content)
                {
                    testEvent.Content = content;
                }
            }
            else if (@event is EventModel.SpanEvent { Content: { } span } spanEvent)
            {
                if (ProcessSpan(span) is { } content)
                {
                    spanEvent.Content = content;
                }
            }

            ((IEventWriter)AgentWriter).WriteEvent(@event);
        }

        internal sealed class LockedManager : TestOptimizationTracerManager, ILockedTracer
        {
            public LockedManager(
                TracerSettings settings,
                IAgentWriter agentWriter,
                IScopeManager scopeManager,
                IStatsdManager statsd,
                RuntimeMetricsWriter runtimeMetricsWriter,
                DirectLogSubmissionManager logSubmissionManager,
                ITelemetryController telemetry,
                IDiscoveryService discoveryService,
                DataStreamsManager dataStreamsManager,
                IGitMetadataTagsProvider gitMetadataTagsProvider,
                ITraceSampler traceSampler,
                ISpanSampler spanSampler,
                IRemoteConfigurationManager remoteConfigurationManager,
                IDynamicConfigurationManager dynamicConfigurationManager,
                ITracerFlareManager tracerFlareManager,
                ISpanEventsManager spanEventsManager)
            : base(
                settings,
                agentWriter,
                scopeManager,
                statsd,
                runtimeMetricsWriter,
                logSubmissionManager,
                telemetry,
                discoveryService,
                dataStreamsManager,
                gitMetadataTagsProvider,
                traceSampler,
                spanSampler,
                remoteConfigurationManager,
                dynamicConfigurationManager,
                tracerFlareManager,
                spanEventsManager)
            {
            }
        }
    }
}
