// <copyright file="AzureDevOpsSourceLinkUrlParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Web;

#pragma warning disable CS1570

namespace Datadog.Trace.Pdb.SourceLink;

internal class AzureDevOpsSourceLinkUrlParser : SourceLinkUrlParser
{
    /// <summary>
    /// Extract the git commit sha and repository url from a Azure DevOps SourceLink mapping string.
    /// For example, for the following SourceLink mapping string:
    ///     https://test.visualstudio.com/test-org/_apis/git/repositories/my-repo/items?api-version=1.0&versionType=commit&version=dd35903c688a74b62d1c6a9e4f41371c65704db8&path=/*
    /// It will return:
    ///     - commit sha: dd35903c688a74b62d1c6a9e4f41371c65704db8
    ///     - repository URL: https://test.visualstudio.com/test-org/_git/my-repo
    /// </summary>
    internal override bool ParseSourceLinkUrl(Uri uri, out string? commitSha, out string? repositoryUrl)
    {
        commitSha = null;
        repositoryUrl = null;

        // Check if the given URI is a valid AzureDevOps SourceLink mapping string.
        if (!uri.AbsolutePath.Contains("_apis/git/repositories/") ||
            !uri.Query.Contains("versionType=commit") ||
            !uri.Query.Contains("version=") ||
            !uri.Query.Contains("path=/*"))
        {
            return false;
        }

        // Extract the query string of the URI and check if the required parameters exist.
        var query = HttpUtility.ParseQueryString(uri.Query);
        commitSha = query["version"];
        if (!IsValidCommitSha(commitSha))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        repositoryUrl = $"https://{uri.Host}/{segments[0]}/_git/{segments[4]}";

        return true;
    }
}
