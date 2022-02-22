// <copyright file="ProbeConfigurationApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Debugger.Configurations;

internal class ProbeConfigurationApi
{
    private const string ProbeConfigurationBackendPath = "api/v2/debugger-cache/configurations";
    private const string HeaderNameApiKey = "DD-API-KEY";
    private const string HeaderNameTrackingId = "X-Datadog-HostId";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ProbeConfigurationApi>();

    private readonly IApiRequestFactory _apiRequestFactory;
    private readonly ImmutableDebuggerSettings _settings;
    private readonly string _probeConfigurationPath;

    private ProbeConfigurationApi(
        string probeConfigurationPath,
        ImmutableDebuggerSettings settings,
        IApiRequestFactory apiRequestFactory)
    {
        _probeConfigurationPath = probeConfigurationPath;
        _apiRequestFactory = apiRequestFactory;
        _settings = settings;
    }

    public static ProbeConfigurationApi Create(
        ImmutableDebuggerSettings settings,
        IApiRequestFactory apiRequestFactory,
        DiscoveryService discoveryService)
    {
        var probeConfigurationPath = settings.ProbeMode switch
        {
            ProbeMode.Backend => $"{settings.ProbeConfigurationsPath}/{ProbeConfigurationBackendPath}",
            ProbeMode.Agent => $"{settings.ProbeConfigurationsPath}/{discoveryService.ProbeConfigurationEndpoint}",
            ProbeMode.File => settings.ProbeConfigurationsPath,
            _ => throw new ArgumentOutOfRangeException()
        };

        return new ProbeConfigurationApi(probeConfigurationPath, settings, apiRequestFactory);
    }

    public async Task<ProbeConfiguration> GetConfigurationsAsync()
    {
        try
        {
            return _settings.ProbeMode switch
            {
                ProbeMode.Backend => await GetConfigurationsFromBackendAsync().ConfigureAwait(false),
                ProbeMode.Agent => await GetConfigurationsFromAgentAsync().ConfigureAwait(false),
                ProbeMode.File => await GetConfigurationsFromFileAsync().ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get configurations");
            return null;
        }
    }

    private async Task<ProbeConfiguration> GetConfigurationsFromBackendAsync()
    {
        var uri = new Uri(_probeConfigurationPath);
        var request = _apiRequestFactory.Create(uri);
        request.AddHeader(HeaderNameApiKey, _settings.ApiKey);
        request.AddHeader(HeaderNameTrackingId, _settings.TrackingId);

        using var response = await request.GetAsync().ConfigureAwait(false);
        var content = await response.ReadAsStringAsync().ConfigureAwait(false);

        if (response.StatusCode is not (>= 200 and <= 299))
        {
            Log.Warning<int, string>("Failed to get configurations with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, content);
            return null;
        }

        var config = ParseJsonApiResponse(content);
        return config;
    }

    private async Task<ProbeConfiguration> GetConfigurationsFromAgentAsync()
    {
        var uri = new Uri(_probeConfigurationPath);
        var request = _apiRequestFactory.Create(uri);
        using var response = await request.GetAsync().ConfigureAwait(false);
        // TODO: validate certificate
        var content = await response.ReadAsStringAsync().ConfigureAwait(false);

        if (response.StatusCode is not (>= 200 and <= 299))
        {
            Log.Warning<int, string>("Failed to get configurations with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, content);
            return null;
        }

        var config = ParseJsonApiResponse(content);
        return config;
    }

    private Task<ProbeConfiguration> GetConfigurationsFromFileAsync()
    {
        var content = File.ReadAllText(_probeConfigurationPath);
        var config = ParseJsonApiResponse(content);

        return Task.FromResult(config);
    }

    private ProbeConfiguration ParseJsonApiResponse(string content)
    {
        var jObject = JsonConvert.DeserializeObject<JObject>(content);
        var data = jObject["data"];
        if (data.IsNullOrEmpty())
        {
            return null;
        }

        var config = data.ParseJsonApiObject<ProbeConfiguration>();
        var included = jObject["included"];
        if (included.IsNullOrEmpty())
        {
            return config;
        }

        var objectMap = included.ToDictionary(token => token["id"].Value<string>());

        config.Probes = data.ParseJsonApiObjects<SnapshotProbe>(relationshipType: "snapshotProbes", objectMap);
        config.MetricProbes = data.ParseJsonApiObjects<MetricProbe>(relationshipType: "metricProbes", objectMap);

        return config;
    }
}
