// <copyright file="BackendProbeConfigurationApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Debugger.Configurations;

internal class BackendProbeConfigurationApi : IProbeConfigurationApi
{
    private const string ProbeConfigurationEndpoint = "api/v2/debugger-cache/configurations";
    private const string HeaderNameApiKey = "DD-API-KEY";
    private const string HeaderNameRuntimeId = "X-Datadog-HostId";

    private readonly IApiRequestFactory _apiRequestFactory;
    private readonly string _targetPath;
    private readonly string _apiKey;
    private readonly string _runtimeId;
    private Uri _uri;

    private BackendProbeConfigurationApi(IApiRequestFactory apiRequestFactory, string targetPath, string apiKey, string runtimeId)
    {
        _apiRequestFactory = apiRequestFactory;
        _targetPath = targetPath;
        _apiKey = apiKey;
        _runtimeId = runtimeId;
    }

    public static BackendProbeConfigurationApi Create(ImmutableDebuggerSettings settings, IApiRequestFactory apiRequestFactory)
    {
        var targetPath =
            settings.ProbeConfigurationsPath.StartsWith("https")
                ? settings.ProbeConfigurationsPath
                : $"https://{settings.ProbeConfigurationsPath}/{ProbeConfigurationEndpoint}/{settings.ServiceName}";

        targetPath += new Dictionary<string, string>
        {
            { "env", settings.Environment },
            { "version", settings.ServiceVersion }
        }.ToQueryString();

        return new BackendProbeConfigurationApi(apiRequestFactory, targetPath, settings.ApiKey, settings.RuntimeId);
    }

    public async Task<ProbeConfiguration> GetConfigurationsAsync()
    {
        _uri ??= new Uri(_targetPath);
        var request = _apiRequestFactory.Create(_uri);
        request.AddHeader(HeaderNameApiKey, _apiKey);
        request.AddHeader(HeaderNameRuntimeId, _runtimeId);

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

        config.SnapshotProbes = data.ParseJsonApiObjects<SnapshotProbe>(relationshipType: "snapshotProbes", objectMap);
        config.MetricProbes = data.ParseJsonApiObjects<MetricProbe>(relationshipType: "metricProbes", objectMap);

        return config;
    }
}
