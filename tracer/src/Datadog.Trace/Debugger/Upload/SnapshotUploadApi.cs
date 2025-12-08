// <copyright file="SnapshotUploadApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Upload
{
    internal class SnapshotUploadApi : DebuggerUploadApiBase
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SnapshotUploadApi>();

        private SnapshotUploadApi(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService? discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            string? staticEndpoint)
            : base(apiRequestFactory, gitMetadataTagsProvider)
        {
            if (!StringUtil.IsNullOrEmpty(staticEndpoint))
            {
                Endpoint = staticEndpoint;
            }
            else if (discoveryService is not null)
            {
                discoveryService.SubscribeToChanges(c =>
                {
                    Endpoint = c.DebuggerV2Endpoint ?? c.DiagnosticsEndpoint;
                    Log.Debug("SnapshotUploadApi: Updated endpoint to {Endpoint}", Endpoint);
                });
            }
            else
            {
                Log.Warning("SnapshotUploadApi: No discovery service or static endpoint available. Snapshots will not be uploaded until an endpoint is configured.");
            }
        }

        public static SnapshotUploadApi Create(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService? discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            string? staticEndpoint)
        {
            return new SnapshotUploadApi(apiRequestFactory, discoveryService, gitMetadataTagsProvider, staticEndpoint);
        }

        public override async Task<bool> SendBatchAsync(ArraySegment<byte> data)
        {
            var uri = BuildUri();
            if (StringUtil.IsNullOrEmpty(uri))
            {
                Log.Warning("Failed to upload snapshot: debugger endpoint not yet retrieved from discovery service");
                return false;
            }

            Log.Debug("SnapshotUploadApi: Sending snapshots to {Uri}", uri);
            using var response = await PostAsync(uri!, data).ConfigureAwait(false);

            if (response.StatusCode is >= 200 and <= 299)
            {
                Log.Debug<string?, int>("Successfully sent snapshots to {Uri}: {StatusCode}", uri, response.StatusCode);
                return true;
            }

            var content = await response.ReadAsStringAsync().ConfigureAwait(false);
            Log.Warning<int, string>("Failed to upload snapshot with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, content);
            return false;
        }
    }
}
