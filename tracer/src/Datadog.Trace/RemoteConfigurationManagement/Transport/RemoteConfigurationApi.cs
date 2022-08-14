// <copyright file="RemoteConfigurationApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Transport
{
    internal class RemoteConfigurationApi : IRemoteConfigurationApi
    {
        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly IDiscoveryService _discoveryService;

        private RemoteConfigurationApi(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService)
        {
            _apiRequestFactory = apiRequestFactory;
            _discoveryService = discoveryService;
        }

        public static RemoteConfigurationApi Create(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService)
        {
            return new RemoteConfigurationApi(apiRequestFactory, discoveryService);
        }

        public async Task<GetRcmResponse> GetConfigs(GetRcmRequest request)
        {
            var uri = _apiRequestFactory.GetEndpoint(_discoveryService.ConfigurationEndpoint);
            var apiRequest = _apiRequestFactory.Create(uri);

            var requestContent = JsonConvert.SerializeObject(request);
            var bytes = Encoding.UTF8.GetBytes(requestContent);
            var payload = new ArraySegment<byte>(bytes);

            var apiResponse = await apiRequest.PostAsync(payload, MimeTypes.Json).ConfigureAwait(false);
            var isRcmDisabled = apiResponse.StatusCode == 404;
            if (isRcmDisabled)
            {
                return null;
            }

            var responseContent = await apiResponse.ReadAsStringAsync().ConfigureAwait(false);
            var response = JsonConvert.DeserializeObject<GetRcmResponse>(responseContent);

            return response;
        }
    }
}
