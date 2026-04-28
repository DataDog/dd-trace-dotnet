// <copyright file="GitMetadataTagsProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;

#nullable enable

namespace Datadog.Trace.Configuration;

internal sealed class GitMetadataTagsProvider : IGitMetadataTagsProvider
{
    private readonly ITelemetryController _telemetry;
    private readonly TracerSettings _immutableTracerSettings;
    private readonly string? _gitCommitSha;
    private readonly string? _gitRepositoryUrl;
    private GitMetadata? _cachedGitTags;

    public GitMetadataTagsProvider(TracerSettings immutableTracerSettings, MutableSettings settings, ITelemetryController telemetry)
    {
        _immutableTracerSettings = immutableTracerSettings;
        // These never change, even though they are exposed on MutableSettings, so we can safely grab them once here
        _gitCommitSha = settings.GitCommitSha;
        _gitRepositoryUrl = settings.GitRepositoryUrl;
        _telemetry = telemetry;
    }

    public bool TryExtractGitMetadata([NotNullWhen(true)] out GitMetadata? gitMetadata)
    {
        if (_immutableTracerSettings.GitMetadataEnabled == false)
        {
            gitMetadata = GitMetadata.Empty;
            return true;
        }

        if (_cachedGitTags != null)
        {
            gitMetadata = _cachedGitTags;
            return true;
        }

        var gitCommitSha = string.IsNullOrWhiteSpace(_gitCommitSha) ? null : _gitCommitSha;
        var gitRepositoryUrl = string.IsNullOrWhiteSpace(_gitRepositoryUrl) ? null : _gitRepositoryUrl;

        gitMetadata = _cachedGitTags = new GitMetadata(gitCommitSha ?? string.Empty, gitRepositoryUrl ?? string.Empty);
        _telemetry.RecordGitMetadata(gitMetadata);
        return true;
    }
}
