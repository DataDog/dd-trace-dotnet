// <copyright file="ApmAgentWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsd;

namespace Datadog.Trace.Ci.Agent;

/// <summary>
/// APM Agent Writer for CI Visibility
/// </summary>
internal class ApmAgentWriter : IEventWriter
{
    private const int DefaultMaxBufferSize = 1024 * 1024 * 10;

    private readonly AgentWriter _agentWriter;

    public ApmAgentWriter(TracerSettings settings, Action<Dictionary<string, float>> updateSampleRates, IDiscoveryService discoveryService, int maxBufferSize = DefaultMaxBufferSize)
    {
        var partialFlushEnabled = settings.PartialFlushEnabled;
        // CI Vis doesn't allow reconfiguration, so don't need to subscribe to changes
        var apiRequestFactory = TracesTransportStrategy.Get(settings.Manager.InitialExporterSettings);
        var statsdManager = new StatsdManager(settings);
        var api = new Api(apiRequestFactory, statsdManager, updateSampleRates, partialFlushEnabled, healthMetricsEnabled: false);
        var statsAggregator = StatsAggregator.Create(api, settings, discoveryService);

        _agentWriter = new AgentWriter(api, statsAggregator, statsdManager, maxBufferSize: maxBufferSize, apmTracingEnabled: settings.ApmTracingEnabled, initialTracerMetricsEnabled: settings.Manager.InitialMutableSettings.TracerMetricsEnabled);
    }

    // Internal for testing
    internal ApmAgentWriter(IApi api, IStatsdManager statsdManager, int maxBufferSize = DefaultMaxBufferSize)
    {
        _agentWriter = new AgentWriter(api, null, statsdManager, maxBufferSize: maxBufferSize);
    }

    public void WriteEvent(IEvent @event)
    {
        // To keep compatibility with the agent version of the payload, any IEvent conversion to span
        // goes here.
        if (CIVisibilityEventsFactory.GetSpan(@event) is { } span)
        {
            WriteTrace(new SpanCollection(span));
        }
    }

    public Task FlushAndCloseAsync()
    {
        return _agentWriter.FlushAndCloseAsync();
    }

    public Task FlushTracesAsync()
    {
        return _agentWriter.FlushTracesAsync();
    }

    public Task<bool> Ping()
    {
        return _agentWriter.Ping();
    }

    public void WriteTrace(in SpanCollection trace)
    {
        _agentWriter.WriteTrace(in trace);
    }
}
