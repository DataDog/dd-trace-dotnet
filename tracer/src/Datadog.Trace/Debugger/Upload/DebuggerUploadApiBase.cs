// <copyright file="DebuggerUploadApiBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.CodeDom;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Processors;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.Upload;

internal abstract class DebuggerUploadApiBase : IBatchUploadApi
{
    protected const string DebuggerV1Endpoint = "debugger/v1/input";

    private readonly IApiRequestFactory _apiRequestFactory;
    private readonly IGitMetadataTagsProvider? _gitMetadataTagsProvider;

    private string? _endpoint = null;
    private string? _tags = null;

    protected DebuggerUploadApiBase(IApiRequestFactory apiRequestFactory, IGitMetadataTagsProvider? gitMetadataTagsProvider)
    {
        _apiRequestFactory = apiRequestFactory;
        _gitMetadataTagsProvider = gitMetadataTagsProvider;
    }

    protected string? Endpoint
    {
        get => _endpoint;
        set => _endpoint = value;
    }

    public abstract Task<bool> SendBatchAsync(ArraySegment<byte> data);

    protected string? BuildUri()
    {
        if (Volatile.Read(ref _endpoint) is not { } endpoint)
        {
            return null;
        }

        var uri = _apiRequestFactory.GetEndpoint(endpoint);
        var builder = new UriBuilder(uri);
        var query = HttpUtility.ParseQueryString(builder.Query);
        _tags ??= GetDefaultTagsMergedWithGlobalTags();
        query["ddtags"] = _tags;
        builder.Query = query.ToString();
        return builder.ToString();
    }

    protected Task<IApiResponse> PostAsync(string uri, ArraySegment<byte> data)
    {
        var request = _apiRequestFactory.Create(new Uri(uri));
        var isDebuggerV1 = uri.Contains(DebuggerV1Endpoint);

        return this is DiagnosticsUploadApi && !isDebuggerV1
                   ? request.PostAsync([new("event", MimeTypes.Json, "event.json", data)])
                   : request.PostAsync(data, MimeTypes.Json);
    }

    private string GetDefaultTagsMergedWithGlobalTags()
    {
        var sb = StringBuilderCache.Acquire();

        try
        {
            // TODO: this only gets the original values, before any updates from remote config or config in code
            // this should be refactored to subscribe to changes instead
            var mutableSettings = Tracer.Instance.Settings.Manager.InitialMutableSettings;
            var environment = TraceUtil.NormalizeTag(mutableSettings.Environment);
            if (!string.IsNullOrEmpty(environment))
            {
                sb.Append($"env:{environment},");
            }

            var version = mutableSettings.ServiceVersion;
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

            if (_gitMetadataTagsProvider != null &&
                _gitMetadataTagsProvider.TryExtractGitMetadata(out var gitMetadata) &&
                gitMetadata != GitMetadata.Empty)
            {
                sb.Append($"{CommonTags.GitRepository}:{gitMetadata.RepositoryUrl},");
                sb.Append($"{CommonTags.GitCommit}:{gitMetadata.CommitSha},");
            }

            foreach (var kvp in mutableSettings.GlobalTags)
            {
                sb.Append($"{kvp.Key}:{kvp.Value},");
            }

            if (sb[sb.Length - 1] == ',')
            {
                sb.Length--;
            }

            return sb.ToString();
        }
        finally
        {
            StringBuilderCache.Release(sb);
        }
    }
}
