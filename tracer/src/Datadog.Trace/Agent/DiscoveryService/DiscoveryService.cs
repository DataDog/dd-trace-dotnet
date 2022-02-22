// <copyright file="DiscoveryService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Agent.DiscoveryService;

/// <summary>
/// Queries datadog-agent and discovers which version we are running against and what endpoints it supports.
/// </summary>
internal class DiscoveryService
{
    private const string DefaultAgentUri = "http://localhost:8126";
    private static readonly string[] SupportedProbeConfigurationEndpoints = new[] { "v0.7/config" };

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DiscoveryService>();

    private readonly IApiRequestFactory _apiRequestFactory;
    private readonly string _agentUri;

    private DiscoveryService(
        IConfigurationSource configurationSource,
        IApiRequestFactory apiRequestFactory)
    {
        _apiRequestFactory = apiRequestFactory;
        _agentUri = configurationSource.GetString(ConfigurationKeys.AgentUri)?.TrimEnd('/') ?? DefaultAgentUri;
    }

    public string Version { get; private set; }

    public string ProbeConfigurationEndpoint { get; private set; }

    public static DiscoveryService Create(IConfigurationSource configurationSource, IApiRequestFactory apiRequestFactory)
    {
        return new DiscoveryService(configurationSource, apiRequestFactory);
    }

    public async Task<bool> DiscoverAsync()
    {
        try
        {
            var api = _apiRequestFactory.Create(new Uri($"{_agentUri}/info"));
            var response = await api.GetAsync().ConfigureAwait(false);
            if (response.StatusCode != 200)
            {
                Log.Error("Failed to discover services");
                return false;
            }

            var content = await response.ReadAsStringAsync().ConfigureAwait(false);
            ProcessDiscoveryResponse(content);

            return true;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to discover services");
            return false;
        }
    }

    private void ProcessDiscoveryResponse(string content)
    {
        var jObject = JsonConvert.DeserializeObject<JObject>(content);
        Version = jObject["version"]?.Value<string>();

        var discoveredEndpoints = jObject["endpoints"]?.Value<string[]>();
        if (discoveredEndpoints == null || discoveredEndpoints.Length == 0)
        {
            return;
        }

        ProbeConfigurationEndpoint = SupportedProbeConfigurationEndpoints
           .FirstOrDefault(
                supportedEndpoint => discoveredEndpoints.Any(
                    endpoint => endpoint.Trim('/').Equals(supportedEndpoint, StringComparison.OrdinalIgnoreCase)));
    }
}
