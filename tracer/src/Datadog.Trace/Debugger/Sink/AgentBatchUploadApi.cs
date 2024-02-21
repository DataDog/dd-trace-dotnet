// <copyright file="AgentBatchUploadApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.Sink
{
    internal class AgentBatchUploadApi : IBatchUploadApi
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AgentBatchUploadApi>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private string? _endpoint = null;
        private string? _tags = null;

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

            var uri = BuildUri(endpoint);
            var request = _apiRequestFactory.Create(new Uri(uri));

            using var response = await request.PostAsync(data, MimeTypes.Json).ConfigureAwait(false);
            if (response.StatusCode is not (>= 200 and <= 299))
            {
                var content = await response.ReadAsStringAsync().ConfigureAwait(false);
                Log.Warning<int, string>("Failed to upload snapshot with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, content);
                return false;
            }

            return true;
        }

        private string BuildUri(string endpoint)
        {
            var uri = _apiRequestFactory.GetEndpoint(endpoint);
            var builder = new UriBuilder(uri);
            var query = HttpUtility.ParseQueryString(builder.Query);
            _tags ??= GetDefaultTagsMergedWithGlobalTags();
            query["ddtags"] = _tags;
            builder.Query = query.ToString();
            return builder.ToString();
        }

        private string GetDefaultTagsMergedWithGlobalTags()
        {
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

            try
            {
                var environment = TraceUtil.NormalizeTag(Tracer.Instance.Settings.EnvironmentInternal);
                if (!string.IsNullOrEmpty(environment))
                {
                    sb.Append($"env:{environment},");
                }

                var version = Tracer.Instance.Settings.ServiceVersionInternal;
                if (!string.IsNullOrEmpty(version))
                {
                    sb.Append($"version:{version},");
                }

                var hostName = PlatformHelpers.HostMetadata.Instance?.Hostname;
                if (!string.IsNullOrEmpty(hostName))
                {
                    sb.Append($"host:{hostName},");
                }

                var runtimeId = Tracer.RuntimeId;
                if (!string.IsNullOrEmpty(runtimeId))
                {
                    sb.Append($"{Tags.RuntimeId}:{runtimeId},");
                }

                var gitRepoUrl = Tracer.Instance.Settings.GitRepositoryUrl;
                if (!string.IsNullOrEmpty(gitRepoUrl))
                {
                    sb.Append($"{Tags.GitRepositoryUrl}:{gitRepoUrl},");
                }

                var gitCommitSha = Tracer.Instance.Settings.GitCommitSha;
                if (!string.IsNullOrEmpty(gitCommitSha))
                {
                    sb.Append($"{Tags.GitCommitSha}:{gitCommitSha},");
                }

                foreach (var kvp in Tracer.Instance.Settings.GlobalTagsInternal)
                {
                    sb.Append($",{kvp.Key}:{kvp.Value}");
                }

                return sb.ToString();
            }
            finally
            {
                StringBuilderCache.Release(sb);
            }
        }
    }
}
