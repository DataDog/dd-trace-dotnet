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

    private static string? BuildRepositoryUrl(Uri uri)
    {
        ReadOnlySpan<char> segment0 = default;
        ReadOnlySpan<char> segment1 = default;
        ReadOnlySpan<char> segment4 = default;
        ReadOnlySpan<char> segment5 = default;
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
                case 0: segment0 = span; break;
                case 1: segment1 = span; break;
                case 4: segment4 = span; break;
                case 5: segment5 = span; break;
            }

            segmentCount++;
        }

        if (segmentCount < 5)
        {
            return null;
        }

        if (uri.Host.EndsWith("visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            // Legacy format: https://{organization}.visualstudio.com
#if NET6_0_OR_GREATER
            return $"https://{uri.Host}/{segment0}/_git/{segment4}";
#else
            return $"https://{uri.Host}/{segment0.ToString()}/_git/{segment4.ToString()}";
#endif
        }

        if (uri.Host.EndsWith("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            // New format: https://dev.azure.com/{organization}
#if NET6_0_OR_GREATER
            return $"https://{uri.Host}/{segment0}/{segment1}/_git/{segment5}";
#else
            return $"https://{uri.Host}/{segment0.ToString()}/{segment1.ToString()}/_git/{segment5.ToString()}";
#endif
        }

        Log.Error("Unsupported Azure DevOps host: {Host}", uri.Host);
        return null;
    }
}
