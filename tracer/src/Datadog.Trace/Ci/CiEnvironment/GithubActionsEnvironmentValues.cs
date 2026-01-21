// <copyright file="GithubActionsEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class GithubActionsEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
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

        JobUrl = $"{serverUrl}/{ValueProvider.GetValue(PlatformKeys.Ci.GitHub.Repository)}/commit/{Commit}/checks";
        JobId = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.Job);
        JobName = ValueProvider.GetValue(PlatformKeys.Ci.GitHub.Job);

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
