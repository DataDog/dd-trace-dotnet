// <copyright file="AzureDevOpsSourceLinkUrlParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Util;

#pragma warning disable CS1570

namespace Datadog.Trace.Pdb.SourceLink;

internal sealed class AzureDevOpsSourceLinkUrlParser : SourceLinkUrlParser
{
    /// <summary>
    ///     Extract the git commit sha and repository url from a Azure DevOps SourceLink mapping string.
    ///     For example, for the following SourceLink mapping string:
    ///     https://test.visualstudio.com/test-org/_apis/git/repositories/my-repo/items?api-version=1.0&amp;versionType=commit
    ///     &amp;version=dd35903c688a74b62d1c6a9e4f41371c65704db8&amp;path=/*
    ///     It will return:
    ///     - commit sha: dd35903c688a74b62d1c6a9e4f41371c65704db8
    ///     - repository URL: https://test.visualstudio.com/test-org/_git/my-repo
    ///     Likewise, for the following SourceLink mapping string:
    ///     https://dev.azure.com/organisation/project/_apis/git/repositories/example.shopping.api/items?api-version=1.0&amp;
    ///     versionType=commit&amp;version=0e4d29442102e6cef1c271025d513c8b2187bcd6&amp;path=/*
    /// It will return:
    ///     - commit sha: 0e4d29442102e6cef1c271025d513c8b2187bcd6
    ///     - repository URL: https://dev.azure.com/organisation/project/_git/example.shopping.api
    /// </summary>
    internal override bool TryParseSourceLinkUrl(Uri uri, [NotNullWhen(true)] out string? commitSha, [NotNullWhen(true)] out string? repositoryUrl)
    {
        commitSha = null;
        repositoryUrl = null;

        try
        {
            // Check if the given URI is a valid AzureDevOps SourceLink mapping string.
            if (!uri.AbsolutePath.Contains("_apis/git/repositories/") ||
                !uri.Query.Contains("versionType=commit") ||
                !uri.Query.Contains("version=") ||
                !uri.Query.Contains("path=/*"))
            {
                return false;
            }

            // Extract the commit sha from the query string.
            ReadOnlySpan<char> shaSpan = default;
            foreach (var pair in uri.Query.SplitIntoSpans('&'))
            {
                ReadOnlySpan<char> pairSpan = pair;
                var eqIndex = pairSpan.IndexOf('=');
                if (eqIndex < 0)
                {
                    continue;
                }

                var key = pairSpan.Slice(0, eqIndex).TrimStart('?');
                if (key.SequenceEqual("version".AsSpan()))
                {
                    shaSpan = pairSpan.Slice(eqIndex + 1);
                    break;
                }
            }

            if (!IsValidCommitSha(shaSpan))
            {
                return false;
            }

            commitSha = shaSpan.ToString();
            repositoryUrl = BuildRepositoryUrl(uri);

            return repositoryUrl is not null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while trying to parse Azure DevOps SourceLink URL");
        }

        return false;
    }

    /// <summary>
    /// Builds the repository URL by locating /_apis/git/repositories/ in the path.
    /// Works for all Azure DevOps variants:
    ///   visualstudio.com: /{project}/_apis/git/repositories/{repo}/items
    ///   dev.azure.com:    /{org}/{project}/_apis/git/repositories/{repo}/items
    ///   TFS on-prem:      [/{vdir}][/{collection}]/{project}/_apis/git/repositories/{repo}/items
    /// The repo URL is everything before _apis, plus /_git/{repo}.
    /// </summary>
    private static string? BuildRepositoryUrl(Uri uri)
    {
        var path = uri.AbsolutePath;

        // Find /_apis/git/repositories/ in the path
        const string marker = "/_apis/git/repositories/";
        var markerPos = path.IndexOf(marker, StringComparison.Ordinal);
        if (markerPos <= 0)
        {
            // markerPos == 0 means nothing before _apis (no project); < 0 means not found
            return null;
        }

        // The prefix path (project and any virtual dir/collection) is everything before /_apis
        var prefixPath = path.Substring(0, markerPos);

        // Extract the repo name after /_apis/git/repositories/
        var afterMarker = path.Substring(markerPos + marker.Length);
        var repoEndSlash = afterMarker.IndexOf('/');
        if (repoEndSlash <= 0)
        {
            return null;
        }

        var repo = afterMarker.Substring(0, repoEndSlash);

        return $"{uri.Scheme}://{uri.Authority}{prefixPath}/_git/{repo}";
    }
}
