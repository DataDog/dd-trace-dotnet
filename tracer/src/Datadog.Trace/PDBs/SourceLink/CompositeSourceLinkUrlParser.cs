// <copyright file="CompositeSourceLinkUrlParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.Pdb.SourceLink
{
    internal class CompositeSourceLinkUrlParser : SourceLinkUrlParser
    {
        /// <summary>
        /// The supported SourceLinkParsers. The ordering is important here - to improve accuracy, the more specific parsers should be listed first.
        /// For example, parsers that do not check for a specific host name should be listed last.
        /// </summary>
        private readonly IEnumerable<SourceLinkUrlParser> _sourceLinkUrlParsers = new SourceLinkUrlParser[]
        {
            new GitHubSourceLinkUrlParser(),
            new BitBucketSourceLinkUrlParser(),
            new AzureDevOpsSourceLinkUrlParser(),
            new GitLabSourceLinkUrlParser()
        };

        public static CompositeSourceLinkUrlParser Instance { get; } = new();

        internal override bool TryParseSourceLinkUrl(Uri uri, [NotNullWhen(true)] out string? commitSha, [NotNullWhen(true)] out string? repositoryUrl)
        {
            foreach (var parser in _sourceLinkUrlParsers)
            {
                if (parser.TryParseSourceLinkUrl(uri, out commitSha, out repositoryUrl))
                {
                    return true;
                }
            }

            commitSha = null;
            repositoryUrl = null;
            return false;
        }
    }
}
