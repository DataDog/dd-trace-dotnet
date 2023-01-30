// <copyright file="GitMetadataTagsProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;

#nullable enable

namespace Datadog.Trace.Configuration;

internal class GitMetadataTagsProvider : IGitMetadataTagsProvider
{
    private readonly ImmutableTracerSettings _immutableTracerSettings;
    private GitMetadata? _cachedGitTags = null;
    private Assembly? _entryAssembly;

    public GitMetadataTagsProvider(ImmutableTracerSettings immutableTracerSettings)
    {
        _immutableTracerSettings = immutableTracerSettings;
    }

    private IDatadogLogger Log { get; } = DatadogLogging.GetLoggerFor(typeof(GitMetadataTagsProvider));

    public bool TryExtractGitMetadata([NotNullWhen(true)] out GitMetadata? gitMetadata)
    {
        try
        {
            if (_immutableTracerSettings.GitMetadataEnabled == false)
            {
                // The user has explicitly disabled tagging telemetry events with Git metadata
                gitMetadata = GitMetadata.Empty;
                return true;
            }

            // Get the tag from configuration. These may originate from the DD_GIT_REPOSITORY_URL and DD_GIT_COMMIT_SHA environment variables,
            // but if those were not available, they may have been extracted from the DD_TAGS environment variable.
            if (string.IsNullOrWhiteSpace(_immutableTracerSettings.GitCommitSha) == false &&
                string.IsNullOrWhiteSpace(_immutableTracerSettings.GitRepositoryUrl) == false)
            {
                gitMetadata = new GitMetadata(_immutableTracerSettings.GitCommitSha, _immutableTracerSettings.GitRepositoryUrl);
                return true;
            }

            if (TryGetGitTagsFromSourceLink(out gitMetadata))
            {
                _cachedGitTags = gitMetadata;
                return true;
            }

            gitMetadata = null;
            return false;
        }
        catch (Exception e)
        {
            Log.Error(e, "Error while extracting SourceLink information", e);
            gitMetadata = GitMetadata.Empty;
            return true;
        }
    }

    /// <summary>
    /// Generate the `git.commit.id` and `git.repository_url` tags by extracting SourceLink information from the entry assembly.
    /// </summary>
    /// <remarks>
    /// The timing in which you call this method is important. For IIS-based web applications, we rely on System.Web.HttpContext.Current
    /// to retrieve the entry assembly, which is only available during the request. Additionally, for OWIN-based web applications running
    /// on IIS, it's possible to call this method before the entry assembly is loaded.
    /// </remarks>
    /// <returns>False if the method was called too early and should be called again later. Otherwise, true.</returns>
    private bool TryGetGitTagsFromSourceLink([NotNullWhen(true)] out GitMetadata? result)
    {
        if (_entryAssembly == null)
        {
            _entryAssembly = EntryAssemblyLocator.GetEntryAssembly();
            if (_entryAssembly == null)
            {
                // Cannot determine the entry assembly. This may mean this method was called too early.
                // Return false to indicate that we should try again later.
                result = default;
                return false;
            }

            if (SourceLinkInformationExtractor.ExtractFromAssemblyAttributes(_entryAssembly, out var commitSha, out var repositoryUrl))
            {
                Log.Information($"Found SourceLink information for assembly {_entryAssembly.GetName().Name} in assembly attributes: commit {commitSha} from {repositoryUrl}");
                result = new GitMetadata(commitSha, repositoryUrl);
                return true;
            }
        }

        // Extracting the SourceLink information from the assembly attributes will only work if:
        // 1. The assembly was built using the .NET Core SDK 2.1.300 or newer or MSBuild 15.7 or newer.
        // 2. The assembly was built using an SDK-Style project file (i.e. <Project Sdk="Microsoft.NET.Sdk">).
        // If these conditions weren't met, the attributes won't be there, so we'll need to extract the information from the PDB file.
        var pdbFullPath = Path.ChangeExtension(_entryAssembly.Location, "pdb");
        if (File.Exists(pdbFullPath))
        {
            if (SourceLinkInformationExtractor.ExtractFromPdb(_entryAssembly, pdbFullPath, out var commitSha, out var repositoryUrl))
            {
                Log.Information($"Found SourceLink information for assembly {_entryAssembly.GetName().Name} in its pdb: commit {commitSha} from {repositoryUrl}");
                result = new GitMetadata(commitSha, repositoryUrl);
                return true;
            }
        }
        else if (pdbFullPath.Contains("Temporary ASP.NET Files"))
        {
            // The PDB file doesn't exist, but this is probably because the application is running in IIS
            // and utilizing Dynamic Compilation. In this scenario, the PDB file will be copied on the fly
            // on the first time an exception is thrown, so we may need to try again later.
            result = null;
            return false;
        }
        else
        {
            Log.Debug("PDB file {PdbFullPath} does not exist", pdbFullPath);
        }

        Log.Information("No SourceLink information found for assembly {AssemblyName}", _entryAssembly.GetName().Name);
        result = GitMetadata.Empty;
        return true;
    }
}
