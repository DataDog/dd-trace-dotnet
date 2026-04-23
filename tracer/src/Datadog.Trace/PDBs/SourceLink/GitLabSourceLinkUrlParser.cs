// <copyright file="GitLabSourceLinkUrlParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.Pdb.SourceLink;

internal sealed class GitLabSourceLinkUrlParser : SourceLinkUrlParser
{
    /// <summary>
    /// Extract the git commit sha and repository url from a GitLab SourceLink mapping string.
    /// Supports both old and new GitLab URL formats, including nested groups/subgroups:
    ///   GitLab &gt;= 12.0: https://{host}/{group}[/{subgroup}/...]/{repo}/-/raw/{sha}/*
    ///   GitLab &lt;  12.0: https://{host}/{group}[/{subgroup}/...]/{repo}/raw/{sha}/*
    /// </summary>
    internal override bool TryParseSourceLinkUrl(Uri uri, [NotNullWhen(true)] out string? commitSha, [NotNullWhen(true)] out string? repositoryUrl)
    {
        commitSha = null;
        repositoryUrl = null;

        try
        {
            var path = uri.AbsolutePath;

            // Try /-/raw/ first (GitLab >= 12.0), then /raw/ (GitLab < 12.0).
            // Use LastIndexOf so that repo paths containing "raw" as a segment name don't confuse us.
            int rawMarkerIndex = path.LastIndexOf("/-/raw/", StringComparison.Ordinal);
            int repoPathEnd;
            int afterRawStart;

            if (rawMarkerIndex >= 0)
            {
                // /-/raw/ found — new format
                repoPathEnd = rawMarkerIndex;
                afterRawStart = rawMarkerIndex + "/-/raw/".Length;
            }
            else
            {
                rawMarkerIndex = path.LastIndexOf("/raw/", StringComparison.Ordinal);
                if (rawMarkerIndex <= 0)
                {
                    // Not found, or /raw/ is at position 0 (which is the GHE pattern, not GitLab)
                    return false;
                }

                repoPathEnd = rawMarkerIndex;
                afterRawStart = rawMarkerIndex + "/raw/".Length;
            }

            // After the raw marker we expect "{sha}/*"
            var afterRaw = path.AsSpan().Slice(afterRawStart);
            var slashIdx = afterRaw.IndexOf('/');
            if (slashIdx <= 0)
            {
                return false;
            }

            var sha = afterRaw.Slice(0, slashIdx);
            var rest = afterRaw.Slice(slashIdx + 1);

            if (!rest.SequenceEqual("*".AsSpan()) || !IsValidCommitSha(sha))
            {
                return false;
            }

            // Require at least 2 non-empty segments before the raw marker (group + repo, or group/sub/repo).
            // After trimming the leading/trailing '/', an inner '/' proves two segments exist.
            var repoPath = path.AsSpan(0, repoPathEnd).TrimStart('/').TrimEnd('/');
            if (repoPath.IndexOf('/') <= 0)
            {
                return false;
            }

            repositoryUrl = $"{uri.Scheme}://{uri.Authority}{path.Substring(0, repoPathEnd)}";
            commitSha = sha.ToString();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while trying to parse GitLab SourceLink URL");
        }

        return false;
    }
}
