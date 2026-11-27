// <copyright file="RemoteConfigurationApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.Util.Streams;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Transport
{
    internal sealed class RemoteConfigurationApi : IRemoteConfigurationApi
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RemoteConfigurationApi));
        private static readonly JsonSerializerSettings SerializerSettings = new();

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly ContainerMetadata _containerMetadata;

        internal RemoteConfigurationApi(IApiRequestFactory apiRequestFactory)
        {
            _apiRequestFactory = apiRequestFactory;
            _containerMetadata = ContainerMetadata.Instance;
        }

        public async Task<GetRcmResponse?> GetConfigs(GetRcmRequest request, string endpoint, CancellationToken cancellationToken = default)
        {
            var apiRequest = CreateRequest(endpoint);
            try
            {
                using var apiResponse = await apiRequest.PostAsJsonAsync(request, MultipartCompression.None, SerializerSettings, cancellationToken).ConfigureAwait(false);
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
                catch (Exception error)
                {
                    throw new RemoteConfigurationDeserializationException(error);
                }
            }
            catch (Exception error) when (cancellationToken.IsCancellationRequested)
            {
                Log.Debug(error, "Remote Configuration request canceled during retirement");
                return null;
            }
        }

#if SHARED_REMOTE_CONFIG
        // The streaming factory uses HttpClient for TCP, named pipes, and Unix sockets.
        public Task<IApiResponse> PostStreamingAsync(string endpoint, ArraySegment<byte> body, CancellationToken cancellationToken)
            => ((HttpClientRequest)CreateRequest(endpoint)).PostStreamingAsync(body, MimeTypes.Json, cancellationToken);
#endif

        private IApiRequest CreateRequest(string endpoint)
        {
            var request = _apiRequestFactory.Create(_apiRequestFactory.GetEndpoint(endpoint));
            request.AddContainerMetadataHeaders(_containerMetadata);
            return request;
        }
    }
}
