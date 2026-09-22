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
    /// The trailing '*' is the literal SourceLink substitution token, not a wildcard.
    /// </summary>
    internal override bool TryParseSourceLinkUrl(Uri uri, [NotNullWhen(true)] out string? commitSha, [NotNullWhen(true)] out string? repositoryUrl)
    {
        commitSha = null;
        repositoryUrl = null;

        try
        {
            var path = uri.AbsolutePath;
            var query = uri.Query;

            bool isBrowseForm;
            ReadOnlySpan<char> repoUrlPath;
            if (path.EndsWith("/raw/*", StringComparison.Ordinal))
            {
                isBrowseForm = false;
                repoUrlPath = path.AsSpan(0, path.Length - "/raw/*".Length);
            }
            else if (path.EndsWith("/browse/*", StringComparison.Ordinal))
            {
                isBrowseForm = true;
                repoUrlPath = path.AsSpan(0, path.Length - "/browse/*".Length);
            }
            else
            {
                return false;
            }

            if (!IsValidRepositoryPath(repoUrlPath))
            {
                return false;
            }

            // Walk the query string once: extract the at={sha} pair and, for browse form, also
            // look for a standalone "raw" flag (as opposed to an unrelated substring match).
            ReadOnlySpan<char> shaSpan = default;
            var hasRawFlag = false;
            foreach (var pair in query.SplitIntoSpans('&'))
            {
                var pairSpan = pair.AsSpan().TrimStart('?');
                var eqIndex = pairSpan.IndexOf('=');
                var key = eqIndex < 0 ? pairSpan : pairSpan.Slice(0, eqIndex);

                if (key.SequenceEqual("at".AsSpan()) && eqIndex >= 0)
                {
                    shaSpan = pairSpan.Slice(eqIndex + 1);
                }
                else if (eqIndex < 0 && key.SequenceEqual("raw".AsSpan()))
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

#if NET6_0_OR_GREATER
            repositoryUrl = $"{uri.Scheme}://{uri.Authority}{repoUrlPath}";
#else
            repositoryUrl = $"{uri.Scheme}://{uri.Authority}{repoUrlPath.ToString()}";
#endif
            commitSha = shaSpan.ToString();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while trying to parse Bitbucket Server SourceLink URL");
        }

        return false;
    }

    private static bool IsValidRepositoryPath(ReadOnlySpan<char> path)
    {
        // Parse the fixed trailing structure from right to left so marker-like base path,
        // project, or repository names do not get mistaken for structural markers.
        var repoStart = path.LastIndexOf('/');
        if (repoStart <= 0 || repoStart == path.Length - 1)
        {
            return false;
        }

        var reposStart = path.Slice(0, repoStart).LastIndexOf('/');
        if (reposStart <= 0 ||
            !path.Slice(reposStart + 1, repoStart - reposStart - 1).SequenceEqual("repos".AsSpan()))
        {
            return false;
        }

        var projectStart = path.Slice(0, reposStart).LastIndexOf('/');
        if (projectStart <= 0 || projectStart == reposStart - 1)
        {
            return false;
        }

        var projectsStart = path.Slice(0, projectStart).LastIndexOf('/');
        return projectsStart >= 0 &&
               path.Slice(projectsStart + 1, projectStart - projectsStart - 1).SequenceEqual("projects".AsSpan());
    }
}
