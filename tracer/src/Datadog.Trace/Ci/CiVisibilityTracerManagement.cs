// <copyright file="CiVisibilityTracerManagement.cs" company="Datadog">
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

namespace Datadog.Trace.Ci;

internal class CiVisibilityTracerManagement : ICiVisibilityTracerManagement
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CiVisibilityTracerManagement));

    // Constructor allows optional initialization of the event platform proxy support.
    public CiVisibilityTracerManagement(EventPlatformProxySupport eventPlatformProxySupport = EventPlatformProxySupport.None)
    {
        EventPlatformProxySupport = eventPlatformProxySupport;
    }

    // Flag indicating whether a locked tracer manager should be used.
    public bool UseLockedTracerManager { get; set; } = true;

    // Property representing the current event platform proxy support mode.
    public EventPlatformProxySupport EventPlatformProxySupport { get; private set; }

    // Returns the CITracerManager if available.
    public CITracerManager? Manager
    {
        get
        {
            if (Tracer.Instance.TracerManager is CITracerManager ciTracerManager)
            {
                return ciTracerManager;
            }

            return null;
        }
    }

    // Checks whether the event platform proxy is supported by the agent using the discovery service.
    public EventPlatformProxySupport IsEventPlatformProxySupportedByAgent(IDiscoveryService discoveryService)
    {
        if (discoveryService is NullDiscoveryService)
        {
            return EventPlatformProxySupport.None;
        }

        Log.Debug("Waiting for agent configuration...");
        var agentConfiguration = new DiscoveryAgentConfigurationCallback(discoveryService).WaitAndGet();
        if (agentConfiguration is null)
        {
            Log.Warning("Discovery service could not retrieve the agent configuration after 5 seconds.");
            return EventPlatformProxySupport.None;
        }

        var eventPlatformProxyEndpoint = agentConfiguration.EventPlatformProxyEndpoint;
        return EventPlatformProxySupportFromEndpointUrl(eventPlatformProxyEndpoint);
    }

    // Determines the event platform proxy support mode based on the endpoint URL.
    public EventPlatformProxySupport EventPlatformProxySupportFromEndpointUrl(string? eventPlatformProxyEndpoint)
    {
        if (!string.IsNullOrEmpty(eventPlatformProxyEndpoint))
        {
            if (eventPlatformProxyEndpoint!.Contains("/v2"))
            {
                Log.Information("Event platform proxy V2 supported by agent.");
                return EventPlatformProxySupport.V2;
            }

            if (eventPlatformProxyEndpoint!.Contains("/v4"))
            {
                Log.Information("Event platform proxy V4 supported by agent.");
                return EventPlatformProxySupport.V4;
            }

            Log.Information("EventPlatformProxyEndpoint: '{EVPEndpoint}' not supported.", eventPlatformProxyEndpoint);
        }
        else
        {
            Log.Information("Event platform proxy is not supported by the agent. Falling back to the APM protocol.");
        }

        return EventPlatformProxySupport.None;
    }

    // Returns an API request factory with a default timeout of 15 seconds.
    public IApiRequestFactory GetRequestFactory(TracerSettings settings)
    {
        return GetRequestFactory(settings, TimeSpan.FromSeconds(15));
    }

    // Returns an API request factory using the specified tracer settings and timeout.
    public IApiRequestFactory GetRequestFactory(TracerSettings tracerSettings, TimeSpan timeout)
    {
        IApiRequestFactory? factory;
        var exporterSettings = tracerSettings.Exporter;
        if (exporterSettings.TracesTransport != TracesTransportType.Default)
        {
            factory = AgentTransportStrategy.Get(
                exporterSettings,
                productName: "CI Visibility",
                tcpTimeout: null,
                AgentHttpHeaderNames.DefaultHeaders,
                () => new TraceAgentHttpHeaderHelper(),
                uri => uri);
        }
        else
        {
#if NETCOREAPP
            Log.Information("Using {FactoryType} for trace transport.", nameof(HttpClientRequestFactory));
            factory = new HttpClientRequestFactory(
                exporterSettings.AgentUri,
                AgentHttpHeaderNames.DefaultHeaders,
                handler: new System.Net.Http.HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                },
                timeout: timeout);
#else
            Log.Information("Using {FactoryType} for trace transport.", nameof(ApiWebRequestFactory));
            factory = new ApiWebRequestFactory(tracerSettings.Exporter.AgentUri, AgentHttpHeaderNames.DefaultHeaders, timeout: timeout);
#endif
            // Use CIVisibilitySettings for proxy configuration.
            var ciVisibilitySettings = CIVisibilitySettings.FromDefaultSources();
            if (!string.IsNullOrWhiteSpace(ciVisibilitySettings.ProxyHttps))
            {
                var proxyHttpsUriBuilder = new UriBuilder(ciVisibilitySettings.ProxyHttps);

                // Extract username and password from the proxy URI.
                var userName = proxyHttpsUriBuilder.UserName;
                var password = proxyHttpsUriBuilder.Password;

                // Clear the username and password from the URI builder.
                proxyHttpsUriBuilder.UserName = string.Empty;
                proxyHttpsUriBuilder.Password = string.Empty;

                if (proxyHttpsUriBuilder.Scheme == "https")
                {
                    // HTTPS proxy is not supported by the .NET BCL.
                    Log.Error("HTTPS proxy is not supported. ({ProxyHttpsUriBuilder})", proxyHttpsUriBuilder);
                    return factory;
                }

                NetworkCredential? credential = null;
                if (!string.IsNullOrWhiteSpace(userName))
                {
                    credential = new NetworkCredential(userName, password);
                }

                Log.Information("Setting proxy to: {ProxyHttps}", proxyHttpsUriBuilder.Uri.ToString());
                factory.SetProxy(new WebProxy(proxyHttpsUriBuilder.Uri, true, ciVisibilitySettings.ProxyNoProxy, credential), credential);
            }
        }

        return factory;
    }

    // Extracts the service name from the provided repository URL.
    public string GetServiceNameFromRepository(string? repository)
    {
        if (!string.IsNullOrEmpty(repository))
        {
            // Remove trailing slash or backslash.
            if (repository!.EndsWith("/") || repository.EndsWith("\\"))
            {
                repository = repository.Substring(0, repository.Length - 1);
            }

            // Use regex to extract the repository name.
            var regex = new Regex(@"[/\\]?([a-zA-Z0-9\-_.]*)$");
            var match = regex.Match(repository);
            if (match is { Success: true, Groups.Count: > 1 })
            {
                const string gitSuffix = ".git";
                var repoName = match.Groups[1].Value;
                if (repoName.EndsWith(gitSuffix))
                {
                    return repoName.Substring(0, repoName.Length - gitSuffix.Length);
                }

                return repoName;
            }
        }

        return string.Empty;
    }

    // Nested class to handle the discovery service configuration callback.
    private class DiscoveryAgentConfigurationCallback
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
            Log.Debug("Agent configuration received.");
        }
    }
}
