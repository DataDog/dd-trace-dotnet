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
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Ci
{
    internal class CITracerManager : TracerManager, ILockedTracer
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CITracerManager>();

        public CITracerManager(ImmutableTracerSettings settings, IAgentWriter agentWriter, ITraceSampler sampler, ISpanSampler spanSampler, IScopeManager scopeManager, IDogStatsd statsd, RuntimeMetricsWriter runtimeMetricsWriter, DirectLogSubmissionManager logSubmissionManager, ITelemetryController telemetry, IDiscoveryService discoveryService, DataStreamsManager dataStreamsManager, string defaultServiceName)
            : base(settings, agentWriter, sampler, spanSampler, scopeManager, statsd, runtimeMetricsWriter, logSubmissionManager, telemetry, discoveryService, dataStreamsManager, defaultServiceName, new Trace.Processors.ITraceProcessor[]
            {
                new Trace.Processors.NormalizerTraceProcessor(),
                new Trace.Processors.TruncatorTraceProcessor(),
                new Processors.TestSuiteVisibilityProcessor(agentWriter is CIAgentlessWriter),
                new Processors.OriginTagTraceProcessor(settings.Exporter.PartialFlushEnabled, agentWriter is CIAgentlessWriter),
            })
        {
        }

        private Span ProcessSpan(Span span)
        {
            if (span is not null)
            {
                foreach (var processor in TraceProcessors)
                {
                    if (processor is not null)
                    {
                        try
                        {
                            span = processor.Process(span);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, e.Message);
                        }
                    }
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
    }
}
