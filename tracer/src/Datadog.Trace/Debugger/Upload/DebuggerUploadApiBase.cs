// <copyright file="DebuggerUploadApiBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Datadog.Trace.Agent;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Processors;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.Upload;

internal abstract class DebuggerUploadApiBase : IBatchUploadApi
{
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

            if (_gitMetadataTagsProvider != null &&
                _gitMetadataTagsProvider.TryExtractGitMetadata(out var gitMetadata) &&
                gitMetadata != GitMetadata.Empty)
            {
                sb.Append($"{CommonTags.GitRepository}:{gitMetadata.RepositoryUrl},");
                sb.Append($"{CommonTags.GitCommit}:{gitMetadata.CommitSha},");
            }

            foreach (var kvp in Tracer.Instance.Settings.GlobalTagsInternal)
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
