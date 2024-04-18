// <copyright file="RemoteConfigurationApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Transport
{
    internal class RemoteConfigurationApi : IRemoteConfigurationApi
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RemoteConfigurationApi));

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly string? _containerId;
        private readonly string? _entityId;
        private string? _configEndpoint = null;

        private RemoteConfigurationApi(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService)
        {
            _apiRequestFactory = apiRequestFactory;
            discoveryService.SubscribeToChanges(
                config =>
                {
                    _configEndpoint = config.ConfigurationEndpoint;
                });

            _containerId = ContainerMetadata.GetContainerId();
            _entityId = ContainerMetadata.GetEntityId();
        }

        public static RemoteConfigurationApi Create(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService)
        {
            return new RemoteConfigurationApi(apiRequestFactory, discoveryService);
        }

        public async Task<GetRcmResponse?> GetConfigs(GetRcmRequest request)
        {
            var configEndpoint = Volatile.Read(ref _configEndpoint);
            if (string.IsNullOrEmpty(configEndpoint))
            {
                Log.Debug("Waiting for discovery service to retrieve configuration.");
                return null;
            }

            var uri = _apiRequestFactory.GetEndpoint(configEndpoint);
            var apiRequest = _apiRequestFactory.Create(uri);

            var requestContent = JsonConvert.SerializeObject(request);
            Log.Debug("Sending Remote Configuration Request: {Content}", requestContent);
            var bytes = Encoding.UTF8.GetBytes(requestContent);
            var payload = new ArraySegment<byte>(bytes);

            if (_containerId != null)
            {
                apiRequest.AddHeader(AgentHttpHeaderNames.ContainerId, _containerId);
            }

            if (_entityId != null)
            {
                apiRequest.AddHeader(AgentHttpHeaderNames.EntityId, _entityId);
            }

            using var apiResponse = await apiRequest.PostAsync(payload, MimeTypes.Json).ConfigureAwait(false);
            var isRcmDisabled = apiResponse.StatusCode == 404;
            if (isRcmDisabled)
            {
                Log.Debug("Remote Configuration has been disabled.");
                return null;
            }

            if (apiResponse.StatusCode is not (>= 200 and <= 299))
            {
                var content = await apiResponse.ReadAsStringAsync().ConfigureAwait(false);
                Log.Warning<int, string>("Failed to receive remote configurations {StatusCode} and message: {ResponseContent}", apiResponse.StatusCode, content);

                return null;
            }

            try
            {
                return await apiResponse.ReadAsTypeAsync<GetRcmResponse>().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new RemoteConfigurationDeserializationException(ex);
            }
        }
    }
}
