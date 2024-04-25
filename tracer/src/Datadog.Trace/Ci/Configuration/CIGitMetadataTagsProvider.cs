// <copyright file="CIGitMetadataTagsProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Ci.Configuration;

internal class CIGitMetadataTagsProvider : IGitMetadataTagsProvider
{
    private readonly ITelemetryController _telemetry;

    public CIGitMetadataTagsProvider(ITelemetryController telemetry)
    {
        _telemetry = telemetry;
    }

    public bool TryExtractGitMetadata([NotNullWhen(true)] out GitMetadata? gitMetadata)
    {
        if (CIEnvironmentValues.Instance is { Commit: { Length: > 0 } commit, Repository: { Length: > 0 } repository })
        {
            gitMetadata = new GitMetadata(commit, repository);
            _telemetry.RecordGitMetadata(gitMetadata);
            return true;
        }

        gitMetadata = null;
        return false;
    }
}
