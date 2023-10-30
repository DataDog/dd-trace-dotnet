// <copyright file="TelemetryTransportManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry.Transports;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Telemetry;

internal class TelemetryTransportManager : IDisposable
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TelemetryTransportManager>();

    internal const int MaxFatalErrors = 2;
    internal const int MaxTransientErrors = 5;
    private readonly TelemetryTransports _transports;
    private readonly IDiscoveryService _discoveryService;
    private ITelemetryTransport _currentTransport;
    private bool? _canSendToAgent = null;

    public TelemetryTransportManager(TelemetryTransports transports, IDiscoveryService discoveryService)
    {
        _transports = transports;
        _discoveryService = discoveryService;
        if (!_transports.HasTransports)
        {
            throw new ArgumentException("Must have at least one transport", nameof(transports));
        }

        discoveryService.SubscribeToChanges(HandleAgentDiscoveryUpdate);

        _currentTransport = GetNextTransport(null);

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug(
                "Telemetry AgentProxy enabled: {AgentProxyEnabled}, Agentless enabled: {AgentlessEnabled}, Agent proxy available {AgentProxyAvailable}. Initial Transport {TransportInfo}",
                _transports.AgentTransport is not null,
                _transports.AgentlessTransport is not null,
                _canSendToAgent switch { true => "Available", false => "Unavailable", _ => "Unknown" },
                _currentTransport.GetTransportInfo());
        }
    }

    public void Dispose()
    {
        _discoveryService.RemoveSubscription(HandleAgentDiscoveryUpdate);
    }

    public async Task<bool> TryPushTelemetry(TelemetryData telemetryData)
    {
        var pushResult = await _currentTransport.PushTelemetry(telemetryData).ConfigureAwait(false);

        if (pushResult == TelemetryPushResult.Success)
        {
            Log.Debug("Successfully sent telemetry");
            return true;
        }

        var previousTransport = _currentTransport;
        _currentTransport = GetNextTransport(previousTransport);

        Log.Debug(
            "Telemetry transport {FailedTransportInfo} failed. Enabling next transport {NextTransportInfo}",
            previousTransport.GetTransportInfo(),
            _currentTransport.GetTransportInfo());
        return false;
    }

    /// <summary>
    /// Internal for testing
    /// </summary>
    internal ITelemetryTransport GetNextTransport(ITelemetryTransport? currentTransport)
    {
        // use agent if we know it's available, use agentless if we know it's not, and use agent as fallback
        if (currentTransport is null)
        {
            return _transports switch
            {
                { AgentTransport: { } t } when _canSendToAgent ?? true => t,
                { AgentlessTransport: { } t } => t,
                { AgentTransport: { } t } => t,
                _ => throw new Exception("Must have at least one transport"),
            };
        }

        var agentProxy = _transports.AgentTransport;
        var agentless = _transports.AgentlessTransport;

        // If only one transport is configured, continue to use it
        // If we're using agentProxy, and agentless is configured, use that
        // If we're using agentless, and agentProxy is configured, and we don't _know_ that we can't use it, use that
        // If no transports are available,
        if (currentTransport == agentProxy)
        {
            return agentless is null
                       ? agentProxy // nothing else available, keep using it
                       : agentless;  // switch from agent to agentless
        }

        Debug.Assert(agentless is not null, "If current transport is not agent, it must be agentless");

        if (agentProxy is null)
        {
            // nothing else available, keep using it
            return agentless!;
        }

        return _canSendToAgent != false
                   ? agentProxy // might be able to send to agent, so try it
                   : agentless!; // the agent is not available, so stick to agentless
    }

    private void HandleAgentDiscoveryUpdate(AgentConfiguration config)
    {
        _canSendToAgent = !string.IsNullOrWhiteSpace(config.TelemetryProxyEndpoint);
        if (_canSendToAgent == true)
        {
            Log.Debug("Telemetry agent proxy is available.");
        }
        else
        {
            Log.Debug("Detected agent does not support telemetry agent proxy");
        }
    }
}
