// <copyright file="AzureDevOpsSourceLinkUrlParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable CS1570

namespace Datadog.Trace.Pdb.SourceLink;

internal class AzureDevOpsSourceLinkUrlParser : SourceLinkUrlParser
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

            // Extract the query string of the URI and check if the required parameters exist.
            var query = ParseQueryString(uri.Query);
            commitSha = query["version"];
            if (!IsValidCommitSha(commitSha))
            {
                return false;
            }

            repositoryUrl = BuildRepositoryUrl(uri);

            return repositoryUrl != null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while trying to parse Azure DevOps SourceLink URL");
        }

        return false;
    }

    private static string? BuildRepositoryUrl(Uri uri)
    {
        var segments = uri.AbsolutePath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 5)
        {
            return null;
        }

        if (uri.Host.EndsWith("visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            // Legacy format: https://{organization}.visualstudio.com
            var project = segments[0];
            var repoName = segments[4];
            return $"https://{uri.Host}/{project}/_git/{repoName}";
        }

        if (uri.Host.EndsWith("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            // New format: https://dev.azure.com/{organization}
            var organization = segments[0];
            var project = segments[1];
            var repoName = segments[5];
            return $"https://{uri.Host}/{organization}/{project}/_git/{repoName}";
        }

        Log.Error("Unsupported Azure DevOps host: {Host}", uri.Host);
        return null;
    }

    private static NameValueCollection ParseQueryString(string queryString)
    {
        // We can't use HttpUtility.ParseQueryString because it would mean taking a dependency on System.Web.
        // Instead, we parse the query string manually by simply splitting on the '&' character,
        // as in our particular use case, there is no need to decode the values.
        var query = new NameValueCollection();
        var pairs = queryString.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split(new char[] { '=' }, 2);
            if (parts.Length == 2)
            {
                query.Add(parts[0], parts[1]);
            }
        }

        return query;
    }
}
