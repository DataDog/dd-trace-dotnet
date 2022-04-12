// <copyright file="RcmProbeConfigurationApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement.Models;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Configurations;

internal class RcmProbeConfigurationApi : IProbeConfigurationApi
{
    private const string ProductType = "LIVE_DEBUGGING";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RcmProbeConfigurationApi>();

    private readonly IApiRequestFactory _apiRequestFactory;
    private readonly string _targetPath;
    private readonly DiscoveryService _discoveryService;
    private readonly ArraySegment<byte> _rcmRequestBody;
    private readonly string _rcmTargetPath;
    private Uri _uri;

    private RcmProbeConfigurationApi(
        string targetPath,
        IApiRequestFactory apiRequestFactory,
        DiscoveryService discoveryService,
        ArraySegment<byte> rcmRequestBodyBody = new(),
        string rcmTargetPath = null)
    {
        _apiRequestFactory = apiRequestFactory;
        _targetPath = targetPath;
        _discoveryService = discoveryService;
        _rcmRequestBody = rcmRequestBodyBody;
        _rcmTargetPath = rcmTargetPath;
    }

    public static RcmProbeConfigurationApi Create(
        ImmutableDebuggerSettings settings,
        IApiRequestFactory apiRequestFactory,
        DiscoveryService discoveryService)
    {
        var rcmRequestBody =
            RcmRequest
               .Create(
                    products: new[] { ProductType },
                    serviceName: settings.ServiceName,
                    serviceVersion: settings.ServiceVersion,
                    environment: settings.Environment,
                    runtimeId: settings.RuntimeId)
               .AsArraySegment();

        var rcmTargetPath = $"datadog/2/{ProductType}/{settings.ServiceName.ToUUID()}/config";

        return new RcmProbeConfigurationApi(settings.ProbeConfigurationsPath, apiRequestFactory, discoveryService, rcmRequestBody, rcmTargetPath);
    }

    public async Task<ProbeConfiguration> GetConfigurationsAsync()
    {
        _uri ??= new Uri($"{_targetPath}/{_discoveryService.ProbeConfigurationEndpoint}");
        var request = _apiRequestFactory.Create(_uri);

        using var response = await request.PostAsync(_rcmRequestBody, MimeTypes.Json).ConfigureAwait(false);
        var content = await response.ReadAsStringAsync().ConfigureAwait(false);
        // TODO: validate certificate

        if (response.StatusCode is not (>= 200 and <= 299))
        {
            Log.Warning<int, string>("Failed to get configurations with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, content);
            return null;
        }

        var config = ParseRcmResponse(content);
        return config;
    }

    private ProbeConfiguration ParseRcmResponse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var rcmResponse = JsonConvert.DeserializeObject<RcmResponse>(content);
        if (rcmResponse.TargetFiles == null || rcmResponse.TargetFiles.Length == 0)
        {
            return null;
        }

        var path = rcmResponse.TargetFiles.FirstOrDefault(file => file.Path.Equals(_rcmTargetPath, StringComparison.OrdinalIgnoreCase));
        if (path == null)
        {
            Log.Warning("No matching probe configurations found in target paths.");
            return null;
        }

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(path.Raw));
        var config = JsonConvert.DeserializeObject<ProbeConfiguration>(decoded);

        return config;
    }
}
