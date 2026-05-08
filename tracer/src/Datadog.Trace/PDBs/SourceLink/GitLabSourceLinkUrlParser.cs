// <copyright file="GitLabSourceLinkUrlParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Util;

namespace Datadog.Trace.Pdb.SourceLink;

internal sealed class GitLabSourceLinkUrlParser : SourceLinkUrlParser
{
    /// <summary>
    /// Extract the git commit sha and repository url from a GitLab SourceLink mapping string.
    /// For example, for the following SourceLink mapping string:
    ///     https://test-gitlab-domain/test-org/test-repo/raw/dd35903c688a74b62d1c6a9e4f41371c65704db8/*
    /// It will return:
    ///     - commit sha: dd35903c688a74b62d1c6a9e4f41371c65704db8
    ///     - repository URL: https://test-gitlab-domain/test-org/test-repo
    /// </summary>
    internal override bool TryParseSourceLinkUrl(Uri uri, [NotNullWhen(true)] out string? commitSha, [NotNullWhen(true)] out string? repositoryUrl)
    {
        commitSha = null;
        repositoryUrl = null;

        try
        {
            ReadOnlySpan<char> org = default;
            ReadOnlySpan<char> repo = default;
            ReadOnlySpan<char> sha = default;
            var segmentCount = 0;

            foreach (var segment in uri.AbsolutePath.SplitIntoSpans('/'))
            {
                ReadOnlySpan<char> span = segment;
                if (span.IsEmpty)
                {
                    continue;
                }

                switch (segmentCount)
                {
                    case 0: org = span; break;
                    case 1: repo = span; break;
                    case 2:
                        if (!span.SequenceEqual("raw".AsSpan()))
                        {
                            return false;
                        }

                        break;
                    case 3: sha = span; break;
                    case 4:
                        if (!span.SequenceEqual("*".AsSpan()))
                        {
                            return false;
                        }

                        break;
                }

                segmentCount++;
            }

            if (segmentCount != 5 || !IsValidCommitSha(sha))
            {
                return false;
            }

#if NET6_0_OR_GREATER
            repositoryUrl = $"{uri.Scheme}://{uri.Authority}/{org}/{repo}";
#else
            repositoryUrl = $"{uri.Scheme}://{uri.Authority}/{org.ToString()}/{repo.ToString()}";
#endif
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
