// <copyright file="SnapshotUploadApi.cs" company="Datadog">
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
    internal class SnapshotUploadApi : DebuggerUploadApiBase
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SnapshotUploadApi>();

        private readonly IApiRequestFactory _apiRequestFactory;

        private SnapshotUploadApi(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider)
            : base(apiRequestFactory, gitMetadataTagsProvider)
        {
            _apiRequestFactory = apiRequestFactory;
            discoveryService.SubscribeToChanges(c => Endpoint = c.DebuggerEndpoint);
        }

        public static SnapshotUploadApi Create(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider)
        {
            return new SnapshotUploadApi(apiRequestFactory, discoveryService, gitMetadataTagsProvider);
        }

        public override async Task<bool> SendBatchAsync(ArraySegment<byte> data)
        {
            var uri = BuildUri();
            if (string.IsNullOrEmpty(uri))
            {
                Log.Warning("Failed to upload snapshot: debugger endpoint not yet retrieved from discovery service");
                return false;
            }

            var request = _apiRequestFactory.Create(new Uri(uri));

            using var response = await request.PostAsync(data, MimeTypes.Json).ConfigureAwait(false);

            if (response.StatusCode is >= 200 and <= 299)
            {
                return true;
            }

            var content = await response.ReadAsStringAsync().ConfigureAwait(false);
            Log.Warning<int, string>("Failed to upload snapshot with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, content);
            return false;
        }
    }
}
