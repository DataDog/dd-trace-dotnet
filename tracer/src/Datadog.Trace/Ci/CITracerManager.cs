// <copyright file="CITracerManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
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
    internal class CITracerManager : TracerManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CITracerManager>();

        public CITracerManager(
            ImmutableTracerSettings settings,
            IAgentWriter agentWriter,
            IScopeManager scopeManager,
            IDogStatsd statsd,
            RuntimeMetricsWriter runtimeMetricsWriter,
            DirectLogSubmissionManager logSubmissionManager,
            ITelemetryController telemetry,
            IDiscoveryService discoveryService,
            DataStreamsManager dataStreamsManager,
            string defaultServiceName,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            ITraceSampler traceSampler,
            ISpanSampler spanSampler,
            IRemoteConfigurationManager remoteConfigurationManager,
            IDynamicConfigurationManager dynamicConfigurationManager,
            ITracerFlareManager tracerFlareManager)
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
                defaultServiceName,
                gitMetadataTagsProvider,
                traceSampler,
                spanSampler,
                remoteConfigurationManager,
                dynamicConfigurationManager,
                tracerFlareManager,
                GetProcessors(settings.ExporterInternal.PartialFlushEnabledInternal, agentWriter is CIVisibilityProtocolWriter))
        {
        }

        private static Trace.Processors.ITraceProcessor[] GetProcessors(bool partialFlushEnabled, bool isCiVisibilityProtocol)
        {
            if (isCiVisibilityProtocol)
            {
                return new Trace.Processors.ITraceProcessor[]
                {
                    new Trace.Processors.NormalizerTraceProcessor(),
                    new Trace.Processors.TruncatorTraceProcessor(),
                    new Processors.OriginTagTraceProcessor(partialFlushEnabled, true),
                };
            }

            return new Trace.Processors.ITraceProcessor[]
            {
                new Trace.Processors.NormalizerTraceProcessor(),
                new Trace.Processors.TruncatorTraceProcessor(),
                new Processors.TestSuiteVisibilityProcessor(),
                new Processors.OriginTagTraceProcessor(partialFlushEnabled, false),
            };
        }

        private Span ProcessSpan(Span span)
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
                    span = processor.Process(span);
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
            if (@event is TestEvent testEvent)
            {
                testEvent.Content = ProcessSpan(testEvent.Content);
            }
            else if (@event is SpanEvent spanEvent)
            {
                spanEvent.Content = ProcessSpan(spanEvent.Content);
            }

            ((IEventWriter)AgentWriter).WriteEvent(@event);
        }

        internal class LockedManager : CITracerManager, ILockedTracer
        {
            public LockedManager(
                ImmutableTracerSettings settings,
                IAgentWriter agentWriter,
                IScopeManager scopeManager,
                IDogStatsd statsd,
                RuntimeMetricsWriter runtimeMetricsWriter,
                DirectLogSubmissionManager logSubmissionManager,
                ITelemetryController telemetry,
                IDiscoveryService discoveryService,
                DataStreamsManager dataStreamsManager,
                string defaultServiceName,
                IGitMetadataTagsProvider gitMetadataTagsProvider,
                ITraceSampler traceSampler,
                ISpanSampler spanSampler,
                IRemoteConfigurationManager remoteConfigurationManager,
                IDynamicConfigurationManager dynamicConfigurationManager,
                ITracerFlareManager tracerFlareManager)
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
                defaultServiceName,
                gitMetadataTagsProvider,
                traceSampler,
                spanSampler,
                remoteConfigurationManager,
                dynamicConfigurationManager,
                tracerFlareManager)
            {
            }
        }
    }
}
