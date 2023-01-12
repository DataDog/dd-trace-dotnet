// <copyright file="SourceLinkTagsProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;

#nullable enable

namespace Datadog.Trace.Configuration
{
    internal class SourceLinkTagsProvider
    {
        private readonly Lazy<Dictionary<string, string>?> _sourceLinkTags;

        private SourceLinkTagsProvider()
        {
            _sourceLinkTags = new Lazy<Dictionary<string, string>?>(GetGitTagsFromSourceLinkInternal);
        }

        private IDatadogLogger Log { get; } = DatadogLogging.GetLoggerFor(typeof(SourceLinkTagsProvider));

        public static SourceLinkTagsProvider Instance { get; } = new();

        /// <summary>
        /// Generate the `git.commit.id` and `git.repository_url` tags by extracting SourceLink information from the entry assembly.
        /// </summary>
        /// <remarks>
        /// The timing in which you call this method is important. For IIS-based web applications, we rely on System.Web.HttpContext.Current
        /// to retrieve the entry assembly, which is only available during the request. Additionally, for OWIN-based web applications running
        /// on IIS, it's possible to call this method before the entry assembly is loaded.
        /// </remarks>
        internal Dictionary<string, string>? GetGitTagsFromSourceLink()
        {
            if (EntryAssemblyLocator.GetEntryAssembly() == null)
            {
                // Cannot determine the entry assembly. This may mean this method was called too early.
                return null;
            }

            return _sourceLinkTags.Value;
        }

        private Dictionary<string, string>? GetGitTagsFromSourceLinkInternal()
        {
            try
            {
                var globalTags = Tracer.Instance.Settings.GlobalTags;
                if (globalTags.ContainsKey(CommonTags.GitCommit) &&
                    globalTags.ContainsKey(CommonTags.GitRepository))
                {
                    Log.Information(
                        "Successfully read `git.commit.sha` and `git.repository_url` from DD_TAGS, " +
                        "so we will use those and avoid trying to extract them from SourceLink information.");
                    return null;
                }

                var assembly = EntryAssemblyLocator.GetEntryAssembly();
                if (assembly == null)
                {
                    Log.Information("Cannot extract SourceLink information - cannot find entry assembly.");
                }
                else if (SourceLinkInformationExtractor.TryGetSourceLinkInfo(assembly, out var commitSha, out var repositoryUrl))
                {
                    Log.Information($"Found SourceLink information for assembly {assembly.GetName().Name}: commit {commitSha} from {repositoryUrl}");
                    return new Dictionary<string, string> { { CommonTags.GitCommit, commitSha }, { CommonTags.GitRepository, repositoryUrl } };
                }
                else
                {
                    Log.Information("No SourceLink information found for assembly {AssemblyName}", assembly.GetName().Name);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while extracting SourceLink information", e);
            }

            return null;
        }
    }
}
