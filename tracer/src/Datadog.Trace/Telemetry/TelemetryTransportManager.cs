// <copyright file="TelemetryTransportManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry.Transports;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Telemetry;

internal class TelemetryTransportManager : IDisposable
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TelemetryTransportManager>();

    internal const int MaxFatalErrors = 2;
    internal const int MaxTransientErrors = 5;
    private readonly IDiscoveryService _discoveryService;
    private readonly IDisposable? _settingSubscription;
    private readonly ITelemetryTransport? _agentlessTransport;
    private ITelemetryTransport? _currentAgentTransport;
    private ITelemetryTransport _currentTransport;
    private bool _agentTransportUpdated;
    private bool? _canSendToAgent = null;

    public TelemetryTransportManager(TracerSettings.SettingsManager settings, TelemetryTransportFactory transports, IDiscoveryService discoveryService)
    {
        if (!transports.HasTransports)
        {
            throw new ArgumentException("Must have at least one transport", nameof(transports));
        }

        _agentlessTransport = transports.AgentlessTransport;
        _discoveryService = discoveryService;

        discoveryService.SubscribeToChanges(HandleAgentDiscoveryUpdate);

        if (transports.AgentTransportFactory is { } agentFactory)
        {
            _currentAgentTransport = agentFactory(settings.InitialExporterSettings);
            _settingSubscription = settings.SubscribeToChanges(changes =>
            {
                if (changes.UpdatedExporter is { } exporter)
                {
                    var newTransport = agentFactory(exporter);
                    Interlocked.Exchange(ref _currentAgentTransport, newTransport);
                    Volatile.Write(ref _agentTransportUpdated, true);
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug("Telemetry AgentProxy updated {TransportInfo}", newTransport.GetTransportInfo());
                    }
                }
            });
        }

        // use agent if we know it's available, use agentless if we know it's not, and use agent as fallback
        _currentTransport = GetNextTransport(null);

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug(
                "Telemetry AgentProxy enabled: {AgentProxyEnabled}, Agentless enabled: {AgentlessEnabled}, Agent proxy available {AgentProxyAvailable}. Initial Transport {TransportInfo}",
                _currentAgentTransport is not null,
                _agentlessTransport is not null,
                _canSendToAgent switch { true => "Available", false => "Unavailable", _ => "Unknown" },
                _currentTransport.GetTransportInfo());
        }
    }

    public void Dispose()
    {
        _settingSubscription?.Dispose();
        _discoveryService.RemoveSubscription(HandleAgentDiscoveryUpdate);
    }

    public async Task<bool> TryPushTelemetry(TelemetryData telemetryData)
    {
        RefreshAgentTransportIfRequired();
        var currentTransport = _currentTransport;
        var pushResult = await currentTransport.PushTelemetry(telemetryData).ConfigureAwait(false);

        if (pushResult == TelemetryPushResult.Success)
        {
            Log.Debug("Successfully sent telemetry");
            return true;
        }

        var previousTransport = currentTransport;
        _currentTransport = GetNextTransport(previousTransport);

        Log.Debug(
            "Telemetry transport {FailedTransportInfo} failed. Enabling next transport {NextTransportInfo}",
            previousTransport.GetTransportInfo(),
            currentTransport.GetTransportInfo());
        return false;
    }

    /// <summary>
    /// Internal for testing
    /// </summary>
    [TestingAndPrivateOnly]
    internal ITelemetryTransport GetNextTransport(ITelemetryTransport? currentTransport)
    {
        // use agent if we know it's available, use agentless if we know it's not, and use agent as fallback
        var agentProxy = Volatile.Read(ref _currentAgentTransport);
        var agentless = _agentlessTransport;
        if (currentTransport is null)
        {
            return agentProxy is not null && (_canSendToAgent ?? true)
                       ? agentProxy
                       : agentless ?? agentProxy ?? throw new Exception("Must have at least one transport");
        }

        // - If only one transport is configured, continue to use it
        // - If we're using agentProxy, and agentless is configured, use that
        // - If we're using agentless, and agentProxy is configured, and we don't _know_ that we can't use it, use that
        if (currentTransport != agentless)
        {
            // Switch from agent to agentless if available, otherwise stick with the same transport,
            return agentless ?? currentTransport;
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

    private void RefreshAgentTransportIfRequired()
    {
        if (!Volatile.Read(ref _agentTransportUpdated))
        {
            return;
        }

        // Refresh required, reset the flag
        Volatile.Write(ref _agentTransportUpdated, false);

        // if we're currently using the agentless transport, just keep using that
        // we also ignore the case where we don't have an agent transport, because this method shouldn't be called in that case
        var currentTransport = _currentTransport;
        var currentAgentTransport = _currentAgentTransport;
        if (currentTransport == _agentlessTransport || currentAgentTransport is null)
        {
            // nothing to do
            return;
        }

        // otherwise, replace the current transport
        _currentTransport = currentAgentTransport;
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
