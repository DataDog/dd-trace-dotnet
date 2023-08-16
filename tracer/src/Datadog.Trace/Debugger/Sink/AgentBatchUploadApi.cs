// <copyright file="AgentBatchUploadApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Sink
{
    internal class AgentBatchUploadApi : IBatchUploadApi
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AgentBatchUploadApi>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private string? _endpoint = null;

        private AgentBatchUploadApi(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService)
        {
            _apiRequestFactory = apiRequestFactory;
            discoveryService.SubscribeToChanges(c => _endpoint = c.DebuggerEndpoint);
        }

        public static AgentBatchUploadApi Create(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService)
        {
            return new AgentBatchUploadApi(apiRequestFactory, discoveryService);
        }

        public async Task<bool> SendBatchAsync(ArraySegment<byte> data)
        {
            if (Volatile.Read(ref _endpoint) is not { } endpoint)
            {
                Log.Warning("Failed to upload snapshot: debugger endpoint not yet retrieved from discovery service");
                return false;
            }

            var uri = _apiRequestFactory.GetEndpoint(endpoint);
            var request = _apiRequestFactory.Create(uri);

            using var response = await request.PostAsync(data, MimeTypes.Json).ConfigureAwait(false);
            if (response.StatusCode is not (>= 200 and <= 299))
            {
                var content = await response.ReadAsStringAsync().ConfigureAwait(false);
                Log.Warning<int, string>("Failed to upload snapshot with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, content);
                return false;
            }

            return true;
        }
    }
}
