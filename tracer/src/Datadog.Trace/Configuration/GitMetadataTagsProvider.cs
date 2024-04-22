// <copyright file="GitMetadataTagsProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;

#nullable enable

namespace Datadog.Trace.Configuration;

internal class GitMetadataTagsProvider : IGitMetadataTagsProvider
{
    private readonly ITelemetryController _telemetry;
    private readonly ImmutableTracerSettings _immutableTracerSettings;
    private readonly IScopeManager _scopeManager;
    private GitMetadata? _cachedGitTags = null;
    private int _tryCount = 0;

    public GitMetadataTagsProvider(ImmutableTracerSettings immutableTracerSettings, IScopeManager scopeManager, ITelemetryController telemetry)
    {
        _immutableTracerSettings = immutableTracerSettings;
        _scopeManager = scopeManager;
        _telemetry = telemetry;
    }

    private IDatadogLogger Log { get; } = DatadogLogging.GetLoggerFor(typeof(GitMetadataTagsProvider));

    /// <summary>
    /// Get the `git.commit.id` and `git.repository_url` tags, either from configuration or from the SourceLink
    /// information embedded in the entry assembly or its PDB.
    /// </summary>
    /// <remarks>
    /// The timing in which you call this method is important. If you call it too early or not in the context an active HTTP request,
    /// we may fail to find the entry assembly, in which case we'll need to try again later.
    /// </remarks>
    /// <returns>False if the method was called too early and should be called again later. Otherwise, true.</returns>
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

            if (_cachedGitTags != null)
            {
                gitMetadata = _cachedGitTags;
                return true;
            }

            string? gitCommitSha = null;
            string? gitRespositoryUrl = null;

            // Get the tag from configuration. These may originate from the DD_GIT_REPOSITORY_URL and DD_GIT_COMMIT_SHA environment variables,
            // but if those were not available, they may have been extracted from the DD_TAGS environment variable.
            if (!string.IsNullOrWhiteSpace(_immutableTracerSettings.GitCommitSha))
            {
                gitCommitSha = _immutableTracerSettings.GitCommitSha!;
            }

            if (!string.IsNullOrWhiteSpace(_immutableTracerSettings.GitRepositoryUrl))
            {
                gitRespositoryUrl = _immutableTracerSettings.GitRepositoryUrl!;
            }

            if (gitCommitSha is not null && gitRespositoryUrl is not null)
            {
                // we have everything
                gitMetadata = _cachedGitTags = new GitMetadata(gitCommitSha, gitRespositoryUrl);
                // For now, we do not need to call the profiler here. The profiler is able to get those information from the environment.
                return true;
            }

            if (TryGetGitTagsFromSourceLink(out gitMetadata))
            {
                if (gitMetadata.IsEmpty)
                {
                    // If data extracted was empty, we try to keep with partial data from DD_TAGS
                    gitMetadata = new GitMetadata(gitCommitSha ?? string.Empty, gitRespositoryUrl ?? string.Empty);
                }

                _cachedGitTags = gitMetadata;
                // These tags could be GitMetadata.Empty but record it anyway, as it gives us an indication
                // that we failed to extract the information
                _telemetry.RecordGitMetadata(gitMetadata);
                PropagateGitMetadataToTheProfiler(gitMetadata);
                return true;
            }

            if (gitCommitSha is not null || gitRespositoryUrl is not null)
            {
                // if we have partial data we go with that
                gitMetadata = _cachedGitTags = new GitMetadata(gitCommitSha ?? string.Empty, gitRespositoryUrl ?? string.Empty);
                return true;
            }

            gitMetadata = null;
            return false;
        }
        catch (Exception e)
        {
            Log.Error(e, "Error while extracting SourceLink information");
            gitMetadata = _cachedGitTags = GitMetadata.Empty;
            return true;
        }
    }

    private void PropagateGitMetadataToTheProfiler(GitMetadata gitMetadata)
    {
        try
        {
            // Avoid P/Invoke if the profiler is not ready (for obvious reason)
            // but also if both repository url and commit sha are empty
            if (Profiler.Instance.Status.IsProfilerReady && !gitMetadata.IsEmpty)
            {
                NativeInterop.SetGitMetadata(RuntimeId.Get(), gitMetadata.RepositoryUrl, gitMetadata.CommitSha);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to share git metadata with the Continuous Profiler.");
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
        if (EntryAssemblyLocator.GetEntryAssembly() is not { } assembly)
        {
            // Cannot determine the entry assembly. This may mean this method was called too early.
            // Return false to indicate that we should try again later.
            // We'll try up to 100 times, but if a span is active, we'll give up immediately, as that means
            // the application is already fully up and running and we're not going to be able to retrieve the entry assembly.
            var nbTries = Interlocked.Increment(ref _tryCount);
            if (nbTries > 100 || _scopeManager.Active?.Span != null)
            {
                Log.Debug("Giving up on trying to locate entry assembly. SourceLink information will not be retrieved.");
                result = GitMetadata.Empty;
                return true;
            }
            else
            {
                Log.Debug("Cannot extract SourceLink information as the entry assembly could not be determined.");
                result = default;
                return false;
            }
        }

        if (SourceLinkInformationExtractor.TryGetSourceLinkInfo(assembly, out var commitSha, out var repositoryUrl))
        {
            Log.Information("Found SourceLink information for assembly {Name}: commit {CommitSha} from {RepositoryUrl}", assembly.GetName().Name, commitSha, repositoryUrl);
            result = new GitMetadata(commitSha, repositoryUrl);
            return true;
        }

        Log.Information("No SourceLink information found for assembly {AssemblyName}", assembly.GetName().Name);
        result = GitMetadata.Empty;
        return true;
    }
}
