// <copyright file="GitHubSourceLinkUrlParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Datadog.Trace.Pdb.SourceLink;

internal class GitHubSourceLinkUrlParser : SourceLinkUrlParser
{
    /// <summary>
    /// Extract the git commit sha and repository url from a GitHub SourceLink mapping string.
    /// For example, for the following SourceLink mapping string:
    ///     https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/dd35903c688a74b62d1c6a9e4f41371c65704db8/*
    /// It will return:
    ///     - commit sha: dd35903c688a74b62d1c6a9e4f41371c65704db8
    ///     - repository URL: https://github.com/DataDog/dd-trace-dotnet
    /// </summary>
    internal override bool TryParseSourceLinkUrl(Uri uri, [NotNullWhen(true)] out string? commitSha, [NotNullWhen(true)] out string? repositoryUrl)
    {
        commitSha = null;
        repositoryUrl = null;

        try
        {
            var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (uri.Host != "raw.githubusercontent.com" || segments.Length != 4 || !IsValidCommitSha(segments[2]))
            {
                return false;
            }

            repositoryUrl = $"https://github.com/{segments[0]}/{segments[1]}";
            commitSha = segments[2];
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while trying to parse GitHub SourceLink URL");
        }

        return false;
    }
}
