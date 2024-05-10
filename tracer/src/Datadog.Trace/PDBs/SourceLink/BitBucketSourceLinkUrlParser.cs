// <copyright file="BitBucketSourceLinkUrlParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Datadog.Trace.Pdb.SourceLink
{
    internal class BitBucketSourceLinkUrlParser : SourceLinkUrlParser
    {
        /// <summary>
        /// Extract the git commit sha and repository url from a BitBucket SourceLink mapping string.
        /// For example, for the following SourceLink mapping string:
        ///     https://api.bitbucket.org/2.0/repositories/my-org/my-repo/src/dd35903c688a74b62d1c6a9e4f41371c65704db8/*
        /// It will return:
        ///     - commit sha: dd35903c688a74b62d1c6a9e4f41371c65704db8
        ///     - repository URL: https://bitbucket.org/test-org/test-repo
        /// </summary>
        internal override bool TryParseSourceLinkUrl(Uri uri, [NotNullWhen(true)] out string? commitSha, [NotNullWhen(true)] out string? repositoryUrl)
        {
            repositoryUrl = null;
            commitSha = null;

            try
            {
                var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (!uri.OriginalString.StartsWith(@"https://api.bitbucket.org/2.0/repositories/") || segments.Length < 6 || !IsValidCommitSha(segments[5]))
                {
                    return false;
                }

                repositoryUrl = $"https://bitbucket.org/{segments[2]}/{segments[3]}";
                commitSha = segments[5];
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while trying to parse BitBucket SourceLink URL");
            }

            return false;
        }
    }
}
