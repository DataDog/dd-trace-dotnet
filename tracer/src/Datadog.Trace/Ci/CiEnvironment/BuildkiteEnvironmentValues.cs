// <copyright file="BuildkiteEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class BuildkiteEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(IGitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Buildkite detected");

        IsCI = true;
        Provider = "buildkite";
        MetricTag = MetricTags.CIVisibilityTestSessionProvider.BuildKite;
        Repository = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.Repo);
        Commit = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.Commit);
        Branch = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.Branch);
        Tag = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.Tag);
        SourceRoot = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.BuildCheckoutPath);
        WorkspacePath = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.BuildCheckoutPath);
        PipelineId = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.BuildId);
        PipelineNumber = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.BuildNumber);
        PipelineName = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.PipelineSlug);
        PipelineUrl = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.BuildUrl);
        JobId = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.JobId);
        JobUrl = string.Format("{0}#{1}", ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.BuildUrl), ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.JobId));

        Message = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.Message);
        AuthorName = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.BuildAuthor);
        AuthorEmail = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.BuildAuthorEmail);
        CommitterName = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.BuildCreator);
        CommitterEmail = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.BuildCreatorEmail);

        // Node
        NodeName = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.AgentId);
        var lstNodeLabels = new List<string>();
        foreach (DictionaryEntry? envvar in ValueProvider.GetValues())
        {
            if (envvar?.Key is string key && key.StartsWith(PlatformKeys.Ci.Buildkite.AgentMetadata, StringComparison.OrdinalIgnoreCase))
            {
                var name = key.Substring(PlatformKeys.Ci.Buildkite.AgentMetadata.Length).ToLowerInvariant();
                var value = envvar?.Value?.ToString();
                lstNodeLabels.Add($"{name}:{value}");
            }
        }

        if (lstNodeLabels.Count > 0)
        {
            NodeLabels = lstNodeLabels.ToArray();
        }

        VariablesToBypass = new Dictionary<string, string?>();
        SetVariablesIfNotEmpty(
            VariablesToBypass,
            PlatformKeys.Ci.Buildkite.BuildId,
            PlatformKeys.Ci.Buildkite.JobId);

        PrBaseBranch = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.PullRequestBaseBranch);
        PrNumber = ValueProvider.GetValue(PlatformKeys.Ci.Buildkite.PullRequestNumber);
    }
}
