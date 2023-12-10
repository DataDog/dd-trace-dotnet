// <copyright file="AgentBatchUploadApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Sink
{
    internal class AgentBatchUploadApi : IBatchUploadApi
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AgentBatchUploadApi>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly ImmutableTracerSettings _immutableTracerSettings;
        private readonly IGitMetadataTagsProvider _gitMetadataTagsProvider;
        private string? _endpoint = null;

        private AgentBatchUploadApi(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService, ImmutableTracerSettings immutableTracerSettings, IGitMetadataTagsProvider gitMetadataTagsProvider)
        {
            _apiRequestFactory = apiRequestFactory;
            _immutableTracerSettings = immutableTracerSettings;
            _gitMetadataTagsProvider = gitMetadataTagsProvider;
            discoveryService.SubscribeToChanges(c =>
            {
                _endpoint = c.DebuggerEndpoint;
            });
        }

        private static Uri AddDDTagsToUri(Uri debuggerEndpoint, IEnumerable<KeyValuePair<string, string>> tagsToAdd)
        {
            var sb = new StringBuilder();
            sb.Append(debuggerEndpoint);
            if (tagsToAdd.Any())
            {
                sb.Append("?ddtags=");
                bool isFirstItem = true;
                foreach (var tag in tagsToAdd)
                {
                    if (isFirstItem)
                    {
                        isFirstItem = false;
                    }
                    else
                    {
                        sb.Append(",");
                    }

                    sb.Append(WebUtility.UrlEncode(tag.Key));
                    sb.Append(":");
                    sb.Append(WebUtility.UrlEncode(tag.Value));
                }
            }

            return new Uri(sb.ToString());
        }

        private IEnumerable<KeyValuePair<string, string>> GetGitTags(IGitMetadataTagsProvider gitMetadataTagsProvider)
        {
            // HACK - this doesn't work because git metadata may be obtained late, and then we're fucked.
            if (gitMetadataTagsProvider.TryExtractGitMetadata(out var gitMetadata) && !gitMetadata.IsEmpty)
            {
                yield return new KeyValuePair<string, string>(CommonTags.GitCommit, gitMetadata.CommitSha);
                yield return new KeyValuePair<string, string>(CommonTags.GitRepository, gitMetadata.RepositoryUrl);
            }
        }

        public static AgentBatchUploadApi Create(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService, ImmutableTracerSettings immutableTracerSettings, IGitMetadataTagsProvider gitMetadataTagsProvider)
        {
            return new AgentBatchUploadApi(apiRequestFactory, discoveryService, immutableTracerSettings, gitMetadataTagsProvider);
        }

        public async Task<bool> SendBatchAsync(ArraySegment<byte> snapshots)
        {
            if (Volatile.Read(ref _endpoint) is not { } endpoint)
            {
                Log.Warning("Failed to upload snapshot: debugger endpoint not yet retrieved from discovery service");
                return false;
            }

            var uri = _apiRequestFactory.GetEndpoint(endpoint);
            uri = AddDDTagsToUri(uri, _immutableTracerSettings.GlobalTagsInternal.Concat(GetGitTags(_gitMetadataTagsProvider)));
            var request = _apiRequestFactory.Create(uri);

            using var response = await request.PostAsync(snapshots, MimeTypes.Json).ConfigureAwait(false);
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
