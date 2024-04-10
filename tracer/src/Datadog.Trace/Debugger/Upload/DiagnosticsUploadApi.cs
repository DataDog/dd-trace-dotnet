// <copyright file="DiagnosticsUploadApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Upload
{
    internal class DiagnosticsUploadApi : DebuggerUploadApiBase
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DiagnosticsUploadApi>();

        private readonly IApiRequestFactory _apiRequestFactory;

        private DiagnosticsUploadApi(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider)
            : base(apiRequestFactory, gitMetadataTagsProvider)
        {
            _apiRequestFactory = apiRequestFactory;
            discoveryService.SubscribeToChanges(c => Endpoint = c.DiagnosticsEndpoint);
        }

        public static DiagnosticsUploadApi Create(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider)
        {
            return new DiagnosticsUploadApi(apiRequestFactory, discoveryService, gitMetadataTagsProvider);
        }

        public override async Task<bool> SendBatchAsync(ArraySegment<byte> data)
        {
            var uri = BuildUri();
            if (string.IsNullOrEmpty(uri))
            {
                Log.Warning("Failed to upload diagnostics: debugger endpoint not yet retrieved from discovery service");
                return false;
            }

            var request = _apiRequestFactory.Create(new Uri(uri));
            var multipartRequest = (IMultipartApiRequest)request;

            using var response =
                await multipartRequest
                     .PostAsync(new MultipartFormItem[]
                      {
                          new("event", MimeTypes.Json, "event.json", data)
                      })
                     .ConfigureAwait(false);

            if (response.StatusCode is >= 200 and <= 299)
            {
                return true;
            }

            var content = await response.ReadAsStringAsync().ConfigureAwait(false);
            Log.Warning<int, string>("Failed to upload diagnostics with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, content);
            return false;
        }
    }
}
