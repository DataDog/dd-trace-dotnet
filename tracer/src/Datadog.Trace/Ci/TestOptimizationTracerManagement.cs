// <copyright file="TestOptimizationTracerManagement.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Ci;

internal sealed class TestOptimizationTracerManagement : ITestOptimizationTracerManagement
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestOptimizationTracerManagement));
    private readonly TestOptimizationSettings _settings;

    public TestOptimizationTracerManagement(
        TestOptimizationSettings settings,
        Func<TestOptimizationSettings, IDiscoveryService>? getDiscoveryServiceFunc,
        bool? useLockedTracerManager)
    {
        _settings = settings;
        DiscoveryService = NullDiscoveryService.Instance;
        if (!settings.Agentless)
        {
            if (!string.IsNullOrWhiteSpace(settings.ForceAgentsEvpProxy))
            {
                // if we force the evp proxy (internal switch)
                if (Enum.TryParse<EventPlatformProxySupport>(settings.ForceAgentsEvpProxy, out var parsedValue))
                {
                    EventPlatformProxySupport = parsedValue;
                    if (settings.DynamicInstrumentationEnabled == true)
                    {
                        DiscoveryService = getDiscoveryServiceFunc?.Invoke(settings) ?? NullDiscoveryService.Instance;
                        if (Log.IsEnabled(LogEventLevel.Debug))
                        {
                            Log.Debug("TestOptimizationTracerManagement: Discovery service set to {DiscoveryServiceType}.", DiscoveryService.GetType().Name);
                        }
                    }
                }
                else if (getDiscoveryServiceFunc != null)
                {
                    DiscoveryService = getDiscoveryServiceFunc(settings);
                    EventPlatformProxySupport = IsEventPlatformProxySupportedByAgent(DiscoveryService);
                }
                else
                {
                    EventPlatformProxySupport = EventPlatformProxySupport.V2;
                }
            }
            else
            {
                DiscoveryService = getDiscoveryServiceFunc?.Invoke(settings) ?? NullDiscoveryService.Instance;
                EventPlatformProxySupport = IsEventPlatformProxySupportedByAgent(DiscoveryService);
            }
        }
        else
        {
            EventPlatformProxySupport = EventPlatformProxySupport.None;
        }

        UseLockedTracerManager = useLockedTracerManager ?? TestOptimization.DefaultUseLockedTracerManager;
    }

    public TestOptimizationTracerManagement(
        TestOptimizationSettings settings,
        EventPlatformProxySupport eventPlatformProxySupport = EventPlatformProxySupport.None,
        bool useLockedTracerManager = true)
    {
        _settings = settings;
        DiscoveryService = NullDiscoveryService.Instance;
        EventPlatformProxySupport =
            eventPlatformProxySupport != EventPlatformProxySupport.None &&
            Enum.TryParse<EventPlatformProxySupport>(settings.ForceAgentsEvpProxy, out var parsedValue)
                ? parsedValue
                : eventPlatformProxySupport;
        UseLockedTracerManager = useLockedTracerManager;
    }

    public EventPlatformProxySupport EventPlatformProxySupport { get; }

    public bool UseLockedTracerManager { get; }

    public IDiscoveryService DiscoveryService { get; }

    public TestOptimizationTracerManager? Manager
    {
        get
        {
            if (Tracer.Instance.TracerManager is TestOptimizationTracerManager ciTracerManager)
            {
                return ciTracerManager;
            }

            return null;
        }
    }

    public EventPlatformProxySupport IsEventPlatformProxySupportedByAgent(IDiscoveryService discoveryService)
    {
        if (discoveryService is NullDiscoveryService)
        {
            return EventPlatformProxySupport.None;
        }

        Log.Debug("TestOptimizationTracerManagement: Waiting for agent configuration...");
        var agentConfiguration = new DiscoveryAgentConfigurationCallback(discoveryService).WaitAndGet(5_000);
        if (agentConfiguration is null)
        {
            Log.Warning("TestOptimizationTracerManagement: Discovery service could not retrieve the agent configuration after 5 seconds.");
            return EventPlatformProxySupport.None;
        }

        var eventPlatformProxyEndpoint = agentConfiguration.EventPlatformProxyEndpoint;
        return EventPlatformProxySupportFromEndpointUrl(eventPlatformProxyEndpoint);
    }

    public EventPlatformProxySupport EventPlatformProxySupportFromEndpointUrl(string? eventPlatformProxyEndpoint)
    {
        if (!string.IsNullOrEmpty(eventPlatformProxyEndpoint))
        {
            if (eventPlatformProxyEndpoint?.Contains("/v2") == true)
            {
                Log.Information("TestOptimizationTracerManagement: Event platform proxy V2 supported by agent.");
                return EventPlatformProxySupport.V2;
            }

            if (eventPlatformProxyEndpoint?.Contains("/v4") == true)
            {
                Log.Information("TestOptimizationTracerManagement: Event platform proxy V4 supported by agent.");
                return EventPlatformProxySupport.V4;
            }

            Log.Information("TestOptimizationTracerManagement: EventPlatformProxyEndpoint: '{EVPEndpoint}' not supported.", eventPlatformProxyEndpoint);
        }
        else
        {
            Log.Information("TestOptimizationTracerManagement: Event platform proxy is not supported by the agent. Falling back to the APM protocol.");
        }

        return EventPlatformProxySupport.None;
    }

    public IApiRequestFactory GetRequestFactory(TracerSettings settings)
    {
        return GetRequestFactory(settings, TimeSpan.FromSeconds(15));
    }

    public IApiRequestFactory GetRequestFactory(TracerSettings tracerSettings, TimeSpan timeout)
    {
        IApiRequestFactory? factory;
        var exporterSettings = tracerSettings.Manager.InitialExporterSettings;
        if (exporterSettings.TracesTransport != TracesTransportType.Default)
        {
            factory = AgentTransportStrategy.Get(
                exporterSettings,
                productName: "CI Visibility",
                tcpTimeout: null,
                defaultAgentHeaders: AgentHttpHeaderNames.DefaultHeaders,
                httpHeaderHelper: new TraceAgentHttpHeaderHelper(),
                getBaseEndpoint: uri => uri);
        }
        else
        {
#if NETCOREAPP
            Log.Information("TestOptimizationTracerManagement: Using {FactoryType} for trace transport.", nameof(HttpClientRequestFactory));
            factory = new HttpClientRequestFactory(
                exporterSettings.AgentUri,
                AgentHttpHeaderNames.DefaultHeaders,
                handler: new System.Net.Http.HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, },
                timeout: timeout);
#else
            Log.Information("TestOptimizationTracerManagement: Using {FactoryType} for trace transport.", nameof(ApiWebRequestFactory));
            factory = new ApiWebRequestFactory(exporterSettings.AgentUri, AgentHttpHeaderNames.DefaultHeaders, timeout: timeout);
#endif
            if (!string.IsNullOrWhiteSpace(_settings.ProxyHttps))
            {
                var proxyHttpsUriBuilder = new UriBuilder(_settings.ProxyHttps!);

                var userName = proxyHttpsUriBuilder.UserName;
                var password = proxyHttpsUriBuilder.Password;

                proxyHttpsUriBuilder.UserName = string.Empty;
                proxyHttpsUriBuilder.Password = string.Empty;

                if (proxyHttpsUriBuilder.Scheme == "https")
                {
                    // HTTPS proxy is not supported by .NET BCL
                    Log.Error("TestOptimizationTracerManagement: HTTPS proxy is not supported. ({ProxyHttpsUriBuilder})", proxyHttpsUriBuilder);
                    return factory;
                }

                NetworkCredential? credential = null;
                if (!string.IsNullOrWhiteSpace(userName))
                {
                    credential = new NetworkCredential(userName, password);
                }

                Log.Information("TestOptimizationTracerManagement: Setting proxy to: {ProxyHttps}", proxyHttpsUriBuilder.Uri.ToString());
                factory.SetProxy(new WebProxy(proxyHttpsUriBuilder.Uri, true, _settings.ProxyNoProxy, credential), credential);
            }
        }

        return factory;
    }

    public string GetServiceNameFromRepository(string? repository)
    {
        if (string.IsNullOrEmpty(repository))
        {
            return string.Empty;
        }

        if (repository!.EndsWith("/") || repository.EndsWith("\\"))
        {
            repository = repository.Substring(0, repository.Length - 1);
        }

        const string gitSuffix = ".git";
        var regex = new Regex(@"[/\\]?([a-zA-Z0-9\-_.]*)$");
        var match = regex.Match(repository);
        if (match is { Success: true, Groups.Count: > 1 })
        {
            var repoName = match.Groups[1].Value;
            return repoName.EndsWith(gitSuffix) ? repoName.Substring(0, repoName.Length - gitSuffix.Length) : repoName;
        }

        return string.Empty;
    }

    private sealed class DiscoveryAgentConfigurationCallback
    {
        private readonly ManualResetEventSlim _manualResetEventSlim;
        private readonly Action<AgentConfiguration> _callback;
        private readonly IDiscoveryService _discoveryService;
        private AgentConfiguration? _agentConfiguration;

        public DiscoveryAgentConfigurationCallback(IDiscoveryService discoveryService)
        {
            _manualResetEventSlim = new ManualResetEventSlim();
            LifetimeManager.Instance.AddShutdownTask(_ => _manualResetEventSlim.Set());
            _discoveryService = discoveryService;
            _callback = Callback;
            _agentConfiguration = null;
            _discoveryService.SubscribeToChanges(_callback);
        }

        // Waits for the agent configuration to be received, up to the specified timeout.
        public AgentConfiguration? WaitAndGet(int timeoutInMs = 5_000)
        {
            _manualResetEventSlim.Wait(timeoutInMs);
            return _agentConfiguration;
        }

        // Callback method invoked when the agent configuration is received.
        private void Callback(AgentConfiguration agentConfiguration)
        {
            _agentConfiguration = agentConfiguration;
            _manualResetEventSlim.Set();
            _discoveryService.RemoveSubscription(_callback);
            Log.Debug("TestOptimizationTracerManagement: Agent configuration received.");
        }
    }
}
