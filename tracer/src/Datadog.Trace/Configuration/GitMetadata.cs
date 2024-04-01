// <copyright file="GitMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Ci.CiEnvironment;

namespace Datadog.Trace.Configuration;

internal class GitMetadata
{
    public static readonly GitMetadata Empty = new(string.Empty, string.Empty);

    public GitMetadata(string commitSha, string repositoryUrl)
    {
        CommitSha = commitSha;
        RepositoryUrl = CIEnvironmentValues.RemoveSensitiveInformationFromUrl(repositoryUrl) ?? string.Empty;
    }

    public string CommitSha { get; }

    public string RepositoryUrl { get; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(CommitSha) && string.IsNullOrWhiteSpace(RepositoryUrl);
}
