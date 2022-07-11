// <copyright file="AgentBatchUploadApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.Sink
{
    internal class AgentBatchUploadApi : IBatchUploadApi
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AgentBatchUploadApi>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly IDiscoveryService _discoveryService;
        private readonly string _targetPath;
        private readonly string _environment;
        private readonly string _version;
        private Uri _uri;

        private AgentBatchUploadApi(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService, string targetPath, string environment, string version)
        {
            _version = version;
            _environment = environment;
            _apiRequestFactory = apiRequestFactory;
            _discoveryService = discoveryService;
            _targetPath = targetPath;
        }

        public static AgentBatchUploadApi Create(ImmutableDebuggerSettings settings, IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService)
        {
            return new AgentBatchUploadApi(apiRequestFactory, discoveryService, settings.SnapshotsPath, settings.Environment, settings.ServiceVersion);
        }

        public async Task<bool> SendBatchAsync(ArraySegment<byte> snapshots)
        {
            var tags = new Dictionary<string, string> { { "env", _environment }, { "version", _version }, { "agent_version", _discoveryService.AgentVersion }, { "debugger_version", TracerConstants.AssemblyVersion } };
            _uri ??= new Uri($"{_targetPath}/{_discoveryService.DebuggerEndpoint}{ToDDTagsQueryString(tags)}");

            var request = _apiRequestFactory.Create(_uri);
            using var response = await request.PostAsync(snapshots, MimeTypes.Json).ConfigureAwait(false);

            if (response.StatusCode is not (>= 200 and <= 299))
            {
                var content = await response.ReadAsStringAsync().ConfigureAwait(false);
                Log.Warning<int, string>("Failed to upload snapshot with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, content);
                return false;
            }

            return true;
        }

        private string ToDDTagsQueryString(IDictionary<string, string> keyValues)
        {
            if (keyValues.Count == 0)
            {
                return string.Empty;
            }

            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
            sb.Append("?ddtags=");
            foreach (var keyValue in keyValues)
            {
                sb.Append(keyValue.Key);
                sb.Append(':');
                sb.Append(keyValue.Value ?? "null");
                sb.Append(',');
            }

            sb.Remove(sb.Length - 1, 1);
            return StringBuilderCache.GetStringAndRelease(sb);
        }
    }
}
