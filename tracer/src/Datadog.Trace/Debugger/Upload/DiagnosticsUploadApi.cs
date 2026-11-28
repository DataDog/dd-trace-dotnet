// <copyright file="DiagnosticsUploadApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Upload
{
    internal sealed class DiagnosticsUploadApi : DebuggerUploadApiBase, IBatchUploadApi
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DiagnosticsUploadApi>();

        internal DiagnosticsUploadApi(DebuggerAgentTransport transport, IGitMetadataTagsProvider gitMetadataTagsProvider)
            : base(transport, static configuration => configuration.DebuggerDiagnosticsUploadEndpoint, gitMetadataTagsProvider)
        {
        }

        public async Task<bool> SendBatchAsync(ArraySegment<byte> data)
        {
            if (BuildTarget() is not { } target || !TryStartPost(target, data, out var post))
            {
                Log.Debug("Failed to upload diagnostics: debugger route unavailable");
                return false;
            }

            using var response = await post.ConfigureAwait(false);
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
