// <copyright file="DiagnosticsUploadApi.cs" company="Datadog">
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
    internal sealed class DiagnosticsUploadApi : DebuggerUploadApiBase
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DiagnosticsUploadApi>();

        private DiagnosticsUploadApi(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider)
            : base(apiRequestFactory, gitMetadataTagsProvider)
        {
            discoveryService.SubscribeToChanges(c =>
            {
                Endpoint = c.DebuggerDiagnosticsUploadEndpoint;
                Log.Debug("DiagnosticsUploadApi: Updated endpoint to {Endpoint}", Endpoint);
            });
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

            using var response = await PostAsync(uri!, data).ConfigureAwait(false);
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
