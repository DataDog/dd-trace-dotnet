// <copyright file="GitlabEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class GitlabEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(IGitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Gitlab CI detected");

        IsCI = true;
        Provider = "gitlab";
        MetricTag = MetricTags.CIVisibilityTestSessionProvider.Gitlab;
        Repository = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.RepositoryUrl);
        Commit = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.CommitSha);
        Branch = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.CommitBranch);
        Tag = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.CommitTag);
        if (string.IsNullOrWhiteSpace(Branch))
        {
            Branch = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.CommitRefName);
        }

        SourceRoot = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.ProjectDir);
        WorkspacePath = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.ProjectDir);

        PipelineId = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.PipelineId);
        PipelineName = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.ProjectPath);
        PipelineNumber = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.PipelineIId);
        PipelineUrl = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.PipelineUrl);

        JobUrl = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.JobUrl);
        JobId = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.JobId);
        JobName = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.JobName);
        StageName = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.JobStage);

        Message = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.CommitMessage);

        var author = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.CommitAuthor);
        var authorArray = author?.Split('<', '>');
        AuthorName = authorArray?[0].Trim();
        AuthorEmail = authorArray?[1].Trim();

        var authorDate = GetDateTimeOffsetVariableIfIsNotEmpty(PlatformKeys.Ci.GitLab.CommitTimestamp, null);
        if (authorDate is not null)
        {
            AuthorDate = authorDate;
        }

        // Node
        NodeName = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.RunnerId);
        if (ValueProvider.GetValue(PlatformKeys.Ci.GitLab.RunnerTags) is { } runnerTags)
        {
            try
            {
                NodeLabels = Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(runnerTags);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error deserializing '{GitlabRunnerTags}' environment variable.", PlatformKeys.Ci.GitLab.RunnerTags);
            }
        }

        VariablesToBypass = new Dictionary<string, string?>();
        SetVariablesIfNotEmpty(
            VariablesToBypass,
            PlatformKeys.Ci.GitLab.ProjectUrl,
            PlatformKeys.Ci.GitLab.PipelineId,
            PlatformKeys.Ci.GitLab.JobId);

        HeadCommit = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.MergeRequestSourceBranchSha);
        PrBaseHeadCommit = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.MergeRequestTargetBranchSha);
        PrBaseCommit = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.MergeRequestDiffBaseSha);
        PrBaseBranch = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.MergeRequestTargetBranchName);
        PrNumber = ValueProvider.GetValue(PlatformKeys.Ci.GitLab.MergeRequestId);
    }
}
