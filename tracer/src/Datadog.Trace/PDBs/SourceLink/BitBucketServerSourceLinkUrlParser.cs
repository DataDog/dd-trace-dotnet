// <copyright file="BitBucketServerSourceLinkUrlParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Util;

namespace Datadog.Trace.Pdb.SourceLink;

internal sealed class BitBucketServerSourceLinkUrlParser : SourceLinkUrlParser
{
    /// <summary>
    /// Extract the git commit sha and repository url from a Bitbucket Server / Data Center SourceLink mapping string.
    /// Supports two URL forms:
    ///   >= 4.7: https://{host}[/base]/projects/{project}/repos/{repo}/raw/*?at={sha}
    ///   &lt;  4.7: https://{host}[/base]/projects/{project}/repos/{repo}/browse/*?at={sha}&amp;raw
    /// </summary>
    internal override bool TryParseSourceLinkUrl(Uri uri, [NotNullWhen(true)] out string? commitSha, [NotNullWhen(true)] out string? repositoryUrl)
    {
        commitSha = null;
        repositoryUrl = null;

        try
        {
            var path = uri.AbsolutePath;
            var query = uri.Query;

            // Bitbucket Server paths are /projects/{project}/repos/{repo}/..., so /projects/ must precede /repos/
            var projectsIdx = path.IndexOf("/projects/", StringComparison.Ordinal);
            var reposIdx = path.IndexOf("/repos/", StringComparison.Ordinal);
            if (projectsIdx < 0 || reposIdx <= projectsIdx)
            {
                return false;
            }

            var afterRepos = path.Substring(reposIdx + "/repos/".Length);

            // Find the repo name (next segment after /repos/)
            var repoEndSlash = afterRepos.IndexOf('/');
            if (repoEndSlash <= 0)
            {
                return false;
            }

            var afterRepo = afterRepos.Substring(repoEndSlash);

            bool isBrowseForm;
            if (afterRepo.StartsWith("/raw/", StringComparison.Ordinal))
            {
                isBrowseForm = false;
            }
            else if (afterRepo.StartsWith("/browse/", StringComparison.Ordinal))
            {
                isBrowseForm = true;
            }
            else
            {
                return false;
            }

            // Walk the query string once: extract the at={sha} pair and, for browse form, also
            // look for a standalone "raw" flag (as opposed to an unrelated substring match).
            ReadOnlySpan<char> shaSpan = default;
            var hasRawFlag = false;
            foreach (var pair in query.SplitIntoSpans('&'))
            {
                ReadOnlySpan<char> pairSpan = ((ReadOnlySpan<char>)pair).TrimStart('?');
                var eqIndex = pairSpan.IndexOf('=');
                var key = eqIndex < 0 ? pairSpan : pairSpan.Slice(0, eqIndex);

                if (key.SequenceEqual("at".AsSpan()) && eqIndex >= 0)
                {
                    shaSpan = pairSpan.Slice(eqIndex + 1);
                }
                else if (key.SequenceEqual("raw".AsSpan()))
                {
                    hasRawFlag = true;
                }
            }

            if (isBrowseForm && !hasRawFlag)
            {
                return false;
            }

            if (!IsValidCommitSha(shaSpan))
            {
                return false;
            }

            // Build repo URL: {scheme}://{authority}[/base]/projects/{project}/repos/{repo}
            // This is everything up to and including the repo name segment
            var repoUrlPath = path.Substring(0, reposIdx + "/repos/".Length + repoEndSlash);

            repositoryUrl = $"{uri.Scheme}://{uri.Authority}{repoUrlPath}";
            commitSha = shaSpan.ToString();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while trying to parse Bitbucket Server SourceLink URL");
        }

        return false;
    }
}
