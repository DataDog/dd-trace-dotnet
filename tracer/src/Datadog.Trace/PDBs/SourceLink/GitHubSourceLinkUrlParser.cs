// <copyright file="GitHubSourceLinkUrlParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Util;

namespace Datadog.Trace.Pdb.SourceLink;

internal sealed class GitHubSourceLinkUrlParser : SourceLinkUrlParser
{
    /// <summary>
    /// Extract the git commit sha and repository url from a GitHub or GitHub Enterprise SourceLink mapping string.
    /// Supports three URL forms:
    ///   1. GitHub.com:              https://raw.githubusercontent.com/{owner}/{repo}/{sha}/*
    ///   2. GHE subdomain isolation: https://raw.{host}/{owner}/{repo}/{sha}/*
    ///   3. GHE main-host /raw/:     https://{host}/raw/{owner}/{repo}/{sha}/*
    /// </summary>
    internal override bool TryParseSourceLinkUrl(Uri uri, [NotNullWhen(true)] out string? commitSha, [NotNullWhen(true)] out string? repositoryUrl)
    {
        commitSha = null;
        repositoryUrl = null;

        try
        {
            // Case 1: GitHub.com — https://raw.githubusercontent.com/{owner}/{repo}/{sha}/*
            if (uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
            {
                return TryParseStandardPath(uri, "https://github.com", out commitSha, out repositoryUrl);
            }

            // Case 2: GHE with subdomain isolation — https://raw.{host}/{owner}/{repo}/{sha}/*
            if (uri.Host.StartsWith("raw.", StringComparison.OrdinalIgnoreCase) && uri.Host.Length > 4)
            {
                var enterpriseHost = uri.Host.Substring(4);
                return TryParseStandardPath(uri, BuildBaseUrl(uri, enterpriseHost), out commitSha, out repositoryUrl);
            }

            // Case 3: GHE without subdomain isolation — https://{host}/raw/{owner}/{repo}/{sha}/*
            return TryParseRawPrefixPath(uri, out commitSha, out repositoryUrl);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while trying to parse GitHub SourceLink URL");
        }

        return false;
    }

    /// <summary>
    /// Parses /{owner}/{repo}/{sha}/* (4 segments) and builds repo URL with the given base.
    /// Used for GitHub.com and GHE with subdomain isolation.
    /// </summary>
    private static bool TryParseStandardPath(Uri uri, string repoUrlBase, out string? commitSha, out string? repositoryUrl)
    {
        commitSha = null;
        repositoryUrl = null;

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
                case 2: sha = span; break;
            }

            segmentCount++;
        }

        if (segmentCount != 4 || !IsValidCommitSha(sha))
        {
            return false;
        }

#if NET6_0_OR_GREATER
        repositoryUrl = $"{repoUrlBase}/{org}/{repo}";
#else
        repositoryUrl = $"{repoUrlBase}/{org.ToString()}/{repo.ToString()}";
#endif
        commitSha = sha.ToString();
        return true;
    }

    /// <summary>
    /// Parses /raw/{owner}/{repo}/{sha}/* (5 segments) for GHE without subdomain isolation.
    /// </summary>
    private static bool TryParseRawPrefixPath(Uri uri, out string? commitSha, out string? repositoryUrl)
    {
        commitSha = null;
        repositoryUrl = null;

        ReadOnlySpan<char> owner = default;
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
                case 0:
                    if (!span.SequenceEqual("raw".AsSpan()))
                    {
                        return false;
                    }

                    break;
                case 1: owner = span; break;
                case 2: repo = span; break;
                case 3: sha = span; break;
            }

            segmentCount++;
        }

        if (segmentCount != 5 || !IsValidCommitSha(sha))
        {
            return false;
        }

        var repoUrlBase = BuildBaseUrl(uri, uri.Host);
#if NET6_0_OR_GREATER
        repositoryUrl = $"{repoUrlBase}/{owner}/{repo}";
#else
        repositoryUrl = $"{repoUrlBase}/{owner.ToString()}/{repo.ToString()}";
#endif
        commitSha = sha.ToString();
        return true;
    }

    private static string BuildBaseUrl(Uri uri, string host)
        => uri.IsDefaultPort ? $"{uri.Scheme}://{host}" : $"{uri.Scheme}://{host}:{uri.Port}";
}
