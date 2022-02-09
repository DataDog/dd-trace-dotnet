// <copyright file="ProbesApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Debugger;

internal class ProbesApi : IProbesApi
{
    private const string HeaderNameApiKey = "DD-API-KEY";
    private const string HeaderNameHostId = "X-Datadog-HostId";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ProbesApi>();

    private readonly IApiRequestFactory _apiRequestFactory;
    private readonly ImmutableDebuggerSettings _settings;

    public ProbesApi(
        ImmutableDebuggerSettings settings,
        IApiRequestFactory apiRequestFactory)
    {
        _apiRequestFactory = apiRequestFactory;
        _settings = settings;
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
        var uri = new Uri(_settings.ProbeConfigurationsPath);
        var request = _apiRequestFactory.Create(uri);
        request.AddHeader(HeaderNameApiKey, _settings.ApiKey);
        request.AddHeader(HeaderNameHostId, _settings.HostId);

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

    private async Task<ProbeConfiguration> GetConfigurationsFromAgentAsync()
    {
        var uri = new Uri(_settings.ProbeConfigurationsPath);
        var request = _apiRequestFactory.Create(uri);
        using var response = await request.GetAsync().ConfigureAwait(false);
        // TODO: validate certificate
        var content = await response.ReadAsStringAsync().ConfigureAwait(false);

        if (response.StatusCode is not (>= 200 and <= 299))
        {
            Log.Warning<int, string>("Failed to get configurations with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, content);
            return null;
        }

        var apiResponse = JsonConvert.DeserializeObject<ProbeConfiguration>(content);
        return apiResponse;
    }

    private Task<ProbeConfiguration> GetConfigurationsFromFileAsync()
    {
        var content = File.ReadAllText(_settings.ProbeConfigurationsPath);
        var config = JsonConvert.DeserializeObject<ProbeConfiguration>(content);

        return Task.FromResult<ProbeConfiguration>(config);
    }

    internal class JsonApiResponse
    {
        public JObject[] Included { get; set; }

        public JObject[] Data { get; set; }
    }
}
