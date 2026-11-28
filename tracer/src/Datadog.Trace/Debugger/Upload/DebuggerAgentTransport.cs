// <copyright file="DebuggerAgentTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Debugger.Upload;

internal sealed class DebuggerAgentTransport : IDisposable
{
    private readonly object _gate = new();
    private readonly TracerSettings.SettingsManager _settings;
    private readonly IDiscoveryService _discoveryService;
    private readonly IDiscoverySnapshot? _discoverySnapshot;
    private readonly Action<AgentConfiguration> _discoveryCallback;
    private readonly Func<ExporterSettings, IApiRequestFactory> _createFactory;
    private readonly IDisposable _settingsSubscription;
    private ExporterSettings _exporter;
    private IApiRequestFactory? _factory;
    private AgentConfiguration? _lastDiscovery;
    private Route? _current;
    private TaskCompletionSource<bool>? _remoteConfigRouteReady;
    private bool _disposed;

    public DebuggerAgentTransport(
        TracerSettings.SettingsManager settings,
        IDiscoveryService discoveryService,
        Func<ExporterSettings, IApiRequestFactory> createFactory)
    {
        _settings = settings;
        _discoveryService = discoveryService;
        _discoverySnapshot = discoveryService as IDiscoverySnapshot;
        _discoveryCallback = UpdateDiscovery;
        _createFactory = createFactory;
        _exporter = settings.InitialExporterSettings;
        _factory = createFactory(_exporter);
        _settingsSubscription = settings.SubscribeToChanges(UpdateExporter);
        try
        {
            if (_discoverySnapshot is not null)
            {
                discoveryService.SubscribeToChanges(_discoveryCallback);
            }
            else
            {
                // Serialize replay for discovery sources that only provide callbacks.
                lock (_gate)
                {
                    discoveryService.SubscribeToChanges(_discoveryCallback);
                }
            }
        }
        catch
        {
            _settingsSubscription.Dispose();
            discoveryService.RemoveSubscription(_discoveryCallback);
            throw;
        }
    }

    internal Route? Current => Volatile.Read(ref _current);

    private bool HasRemoteConfigRoute
        => _current is { HasRemoteConfigEndpoints: true } route &&
           ReferenceEquals(route.Configuration.DiscoverySettings, _settings.CurrentExporterSettings) &&
           ReferenceEquals(route.Configuration, _discoverySnapshot?.CurrentConfiguration);

    internal Task<bool> WaitForRemoteConfigRouteAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return Task.FromResult(false);
            }

            return HasRemoteConfigRoute
                       ? Task.FromResult(true)
                       : (_remoteConfigRouteReady ??= new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }
    }

    internal bool TryRunWithRemoteConfigRoute(Action apply)
        => _discoverySnapshot is not null &&
           Current is { HasRemoteConfigEndpoints: true } route &&
           TryRunWithCurrentRoute(route, apply);

    internal bool TryAdmitUpload(Route route)
        => TryRunWithCurrentRoute(route, static () => { });

    private bool TryRunWithCurrentRoute(Route route, Action apply)
    {
        if (route.Configuration.DiscoverySettings is not { } exporter)
        {
            return false;
        }

        return _settings.TryRunWithCurrentExporter(
            exporter,
            () => _discoverySnapshot is { } snapshot
                      ? snapshot.TryRunWithCurrentConfiguration(configuration => ReferenceEquals(configuration, route.Configuration) && ApplyIfCurrent())
                      : ApplyIfCurrent());

        bool ApplyIfCurrent()
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_current, route))
                {
                    return false;
                }

                apply();
                return true;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Volatile.Write(ref _current, null);
            _remoteConfigRouteReady?.TrySetResult(false);
            _remoteConfigRouteReady = null;
        }

        _settingsSubscription.Dispose();
        _discoveryService.RemoveSubscription(_discoveryCallback);
    }

    private void UpdateExporter(TracerSettings.SettingsManager.SettingChanges changes)
    {
        var exporter = changes.EffectiveExporter;
        lock (_gate)
        {
            if (_disposed || (ReferenceEquals(_exporter, exporter) && _factory is not null))
            {
                return;
            }

            Volatile.Write(ref _current, null);
            _factory = null;
            _exporter = exporter;
            _factory = _createFactory(exporter);
            if (_lastDiscovery is { } discovered && ReferenceEquals(discovered.DiscoverySettings, exporter))
            {
                SetCurrentRoute(_factory, discovered);
            }
        }
    }

    private void UpdateDiscovery(AgentConfiguration discovered)
    {
        lock (_gate)
        {
            if (_disposed ||
                (_discoverySnapshot is { } snapshot && !ReferenceEquals(discovered, snapshot.CurrentConfiguration)))
            {
                return;
            }

            // Discovery can complete before this transport receives the exporter update.
            _lastDiscovery = discovered;
            if (ReferenceEquals(discovered.DiscoverySettings, _exporter) && _factory is { } factory)
            {
                SetCurrentRoute(factory, discovered);
            }
        }
    }

    private void SetCurrentRoute(IApiRequestFactory factory, AgentConfiguration configuration)
    {
        if (_current is not { } route ||
            !ReferenceEquals(route.Factory, factory) ||
            !ReferenceEquals(route.Configuration, configuration))
        {
            Volatile.Write(ref _current, new Route(factory, configuration));
        }

        if (_remoteConfigRouteReady is { } ready && HasRemoteConfigRoute)
        {
            _remoteConfigRouteReady = null;
            ready.TrySetResult(true);
        }
    }

    internal sealed class Route(IApiRequestFactory factory, AgentConfiguration configuration)
    {
        public IApiRequestFactory Factory { get; } = factory;

        public AgentConfiguration Configuration { get; } = configuration;

        public bool HasRemoteConfigEndpoints => !string.IsNullOrEmpty(Configuration.ConfigurationEndpoint) && !string.IsNullOrEmpty(Configuration.DebuggerEndpoint);
    }
}
