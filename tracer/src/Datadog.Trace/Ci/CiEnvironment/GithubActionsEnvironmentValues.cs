// <copyright file="GithubActionsEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class GithubActionsEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    // Well-known diagnostic directory locations for GitHub Actions runners
    // Linux paths (GitHub-hosted runners use /home/runner/actions-runner)
    private static readonly string[] LinuxDiagnosticDirs =
    [
        "/home/runner/actions-runner/cached/_diag", // GitHub-hosted runners (SaaS) with cached directory
        "/home/runner/actions-runner/_diag",        // Self-hosted runners
    ];

    // macOS paths (GitHub-hosted macOS runners use /Users/runner/actions-runner)
    private static readonly string[] MacOSDiagnosticDirs =
    [
        "/Users/runner/actions-runner/cached/_diag",
        "/Users/runner/actions-runner/_diag",
    ];

    // Regex fallback for log files with embedded JSON (handles multi-line)
    // Matches: "k": "check_run_id" ... "v": 55411116365
    private static readonly Regex CheckRunIdRegex = new(
        @"""k""\s*:\s*""check_run_id""\s*,\s*""v""\s*:\s*(\d+)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static string[] GetDiagnosticDirectories()
    {
        var dirs = new List<string>();

        switch (FrameworkDescription.Instance.OSPlatform)
        {
            case OSPlatformName.Windows:
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                dirs.Add(Path.Combine(programFiles, "actions-runner", "cached", "_diag"));
                dirs.Add(Path.Combine(programFiles, "actions-runner", "_diag"));
                dirs.Add(@"C:\actions-runner\cached\_diag");
                dirs.Add(@"C:\actions-runner\_diag");
                break;

            case OSPlatformName.MacOS:
                dirs.AddRange(MacOSDiagnosticDirs);
                break;

            case OSPlatformName.Linux:
            default:
                dirs.AddRange(LinuxDiagnosticDirs);
                break;
        }

        return dirs.ToArray();
    }

    private static bool TryExtractFromJson(string content, out string? jobId)
    {
        jobId = null;

        try
        {
            var jsonObject = JObject.Parse(content);

            // Navigate: job.d[] where k == "check_run_id", get v
            // Structure: { "job": { "d": [ { "k": "check_run_id", "v": 55411116365.0 } ] } }
            var jobData = jsonObject["job"]?["d"] as JArray;
            if (jobData != null)
            {
                foreach (var item in jobData)
                {
                    if (item["k"]?.Value<string>() == "check_run_id")
                    {
                        var value = item["v"]?.Value<long>();
                        if (value.HasValue && value.Value > 0)
                        {
                            jobId = value.Value.ToString(CultureInfo.InvariantCulture);
                            return true;
                        }
                    }
                }
            }
        }
        catch
        {
            // Not valid JSON - fall back to regex
        }

        return false;
    }

    private static bool TryExtractFromRegex(string content, out string? jobId)
    {
        jobId = null;

        // Regex handles multi-line JSON and log files with embedded JSON fragments
        var match = CheckRunIdRegex.Match(content);
        if (match.Success && match.Groups.Count > 1)
        {
            var value = match.Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                jobId = value;
                return true;
            }
        }

        return false;
    }

    protected override void OnInitialize(IGitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: GitHub Actions detected");

        IsCI = true;
        Provider = "github";
        MetricTag = MetricTags.CIVisibilityTestSessionProvider.GithubActions;

        var serverUrl = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.ServerUrl);
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            serverUrl = "https://github.com";
        }

        serverUrl = RemoveSensitiveInformationFromUrl(serverUrl);

        var rawRepository = $"{serverUrl}/{ValueProvider.GetValue(PlatformKeys.Ci.GitHub.Repository)}";
        Repository = $"{rawRepository}.git";
        Commit = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.Sha);

        var headRef = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.HeadRef);
        var ghRef = !string.IsNullOrEmpty(headRef) ? headRef : ValueProvider.GetValue(PlatformKeys.Ci.GitHub.Ref);
        if (ghRef?.Contains("tags") == true)
        {
            Tag = ghRef;
        }
        else
        {
            Branch = ghRef;
        }

        SourceRoot = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.Workspace);
        WorkspacePath = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.Workspace);
        PipelineId = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.RunId);
        PipelineNumber = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.RunNumber);
        PipelineName = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.Workflow);
        var attempts = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.RunAttempt);
        if (string.IsNullOrWhiteSpace(attempts))
        {
            PipelineUrl = $"{rawRepository}/actions/runs/{PipelineId}";
        }
        else
        {
            PipelineUrl = $"{rawRepository}/actions/runs/{PipelineId}/attempts/{attempts}";
        }

        // Job name is always from GITHUB_JOB
        JobName = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.Job);

        // Try to get the numeric job ID for proper URL construction
        // Priority: 1. JOB_CHECK_RUN_ID env var, 2. Diagnostics file, 3. GITHUB_JOB (fallback)
        var numericJobId = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.JobCheckRunId);

        if (string.IsNullOrWhiteSpace(numericJobId) &&
            TryGetJobIdFromDiagnosticsFile(out var diagJobId))
        {
            numericJobId = diagJobId;
        }

        if (!string.IsNullOrWhiteSpace(numericJobId))
        {
            // We have a numeric job ID - construct the correct job-specific URL
            JobId = numericJobId;
            JobUrl = $"{rawRepository}/actions/runs/{PipelineId}/job/{numericJobId}";
            Log.Debug("GitHub Actions job URL constructed with numeric job ID: {JobUrl}", JobUrl);
        }
        else
        {
            // Fallback to current behavior - use job name and commit checks URL
            JobId = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.Job);
            JobUrl = $"{serverUrl}/{ValueProvider.GetValue(PlatformKeys.Ci.GitHub.Repository)}/commit/{Commit}/checks";
            Log.Debug("GitHub Actions job URL using fallback commit checks format: {JobUrl}", JobUrl);
        }

        VariablesToBypass = new Dictionary<string, string?>();
        SetVariablesIfNotEmpty(
            VariablesToBypass,
            [
                PlatformKeys.Ci.GitHub.ServerUrl,
                PlatformKeys.Ci.GitHub.Repository,
                PlatformKeys.Ci.GitHub.RunId,
                PlatformKeys.Ci.GitHub.RunAttempt
            ],
            kvp =>
            {
                if (kvp.Key == PlatformKeys.Ci.GitHub.ServerUrl)
                {
                    return RemoveSensitiveInformationFromUrl(kvp.Value);
                }

                return kvp.Value;
            });

        // Load github-event.json
        LoadGithubEventJson();
        if (string.IsNullOrEmpty(PrBaseBranch))
        {
            PrBaseBranch = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.BaseRef);
        }
    }

    /// <summary>
    /// Attempts to read the numeric job ID from GitHub Actions runner diagnostics files.
    /// This is a fallback mechanism when JOB_CHECK_RUN_ID environment variable is not set.
    /// </summary>
    /// <param name="jobId">The numeric job ID if found.</param>
    /// <returns>True if the job ID was successfully extracted, false otherwise.</returns>
    private bool TryGetJobIdFromDiagnosticsFile(out string? jobId)
    {
        jobId = null;
        var diagnosticDirs = GetDiagnosticDirectories();

        foreach (var diagDir in diagnosticDirs)
        {
            try
            {
                if (!Directory.Exists(diagDir))
                {
                    continue;
                }

                // Look for Worker_*.log files, sorted by modification time (newest first)
                // to get the most relevant/current job information
                var workerLogFiles = Directory.GetFiles(diagDir, "Worker_*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Select(f => f.FullName);

                foreach (var logFile in workerLogFiles)
                {
                    if (TryExtractJobIdFromFile(logFile, out jobId))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error accessing diagnostics directory: {Directory}", diagDir);
            }
        }

        Log.Debug("Could not extract GitHub Actions job ID from diagnostics files");
        return false;
    }

    private bool TryExtractJobIdFromFile(string logFilePath, out string? jobId)
    {
        jobId = null;

        try
        {
            // Skip files larger than 10MB to avoid memory issues with unexpected large files
            const long maxFileSizeBytes = 10 * 1024 * 1024;
            var fileInfo = new FileInfo(logFilePath);
            if (fileInfo.Length > maxFileSizeBytes)
            {
                Log.Debug("Skipping Worker log file (too large: {Size} bytes): {FilePath}", fileInfo.Length, logFilePath);
                return false;
            }

            var content = File.ReadAllText(logFilePath);

            // Strategy 1: Try parsing as pure JSON first (handles well-formed JSON files)
            if (TryExtractFromJson(content, out jobId))
            {
                Log.Debug("GitHub Actions check_run_id extracted via JSON parse: {JobId} from {FilePath}", jobId, logFilePath);
                return true;
            }

            // Strategy 2: Fall back to regex (handles log files with embedded JSON, multi-line content)
            if (TryExtractFromRegex(content, out jobId))
            {
                Log.Debug("GitHub Actions check_run_id extracted via regex: {JobId} from {FilePath}", jobId, logFilePath);
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error reading Worker log file: {FilePath}", logFilePath);
        }

        return false;
    }

    private void LoadGithubEventJson()
    {
        // Load github-event.json
        try
        {
            var githubEventPath = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.EventPath);
            if (!string.IsNullOrWhiteSpace(githubEventPath))
            {
                var githubEvent = File.ReadAllText(githubEventPath);
                var githubEventObject = JObject.Parse(githubEvent);
                var number = githubEventObject["number"]?.Value<int>();
                if (number is > 0)
                {
                    PrNumber = number.Value.ToString(CultureInfo.InvariantCulture);
                }

                var pullRequestObject = githubEventObject["pull_request"];
                if (pullRequestObject is not null)
                {
                    var prHeadSha = pullRequestObject["head"]?["sha"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(prHeadSha))
                    {
                        HeadCommit = prHeadSha;
                    }

                    var prBaseSha = pullRequestObject["base"]?["sha"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(prBaseSha))
                    {
                        PrBaseHeadCommit = prBaseSha;
                    }

                    var prBaseRef = pullRequestObject["base"]?["ref"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(prBaseRef))
                    {
                        PrBaseBranch = prBaseRef;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            TestOptimization.Instance.Log.Warning(ex, "Error loading the github-event.json");
        }
    }
}
