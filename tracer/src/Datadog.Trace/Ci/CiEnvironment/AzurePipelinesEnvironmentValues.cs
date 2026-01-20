// <copyright file="AzurePipelinesEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class AzurePipelinesEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(IGitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Azure Pipelines detected");

        IsCI = true;
        Provider = "azurepipelines";
        MetricTag = MetricTags.CIVisibilityTestSessionProvider.AzurePipelines;
        SourceRoot = ValueProvider.GetValue(PlatformKeys.Ci.Azure.BuildSourcesDirectory);
        WorkspacePath = ValueProvider.GetValue(PlatformKeys.Ci.Azure.BuildSourcesDirectory);
        PipelineId = ValueProvider.GetValue(PlatformKeys.Ci.Azure.BuildBuildId);
        PipelineName = ValueProvider.GetValue(PlatformKeys.Ci.Azure.BuildDefinitionName);
        PipelineNumber = ValueProvider.GetValue(PlatformKeys.Ci.Azure.BuildBuildId);
        PipelineUrl = string.Format(
            "{0}{1}/_build/results?buildId={2}",
            ValueProvider.GetValue(PlatformKeys.Ci.Azure.SystemTeamFoundationServerUri),
            ValueProvider.GetValue(PlatformKeys.Ci.Azure.SystemTeamProjectId),
            ValueProvider.GetValue(PlatformKeys.Ci.Azure.BuildBuildId));

        StageName = ValueProvider.GetValue(PlatformKeys.Ci.Azure.SystemStageDisplayName);

        JobId = ValueProvider.GetValue(PlatformKeys.Ci.Azure.SystemJobId);
        JobName = ValueProvider.GetValue(PlatformKeys.Ci.Azure.SystemJobDisplayName);
        JobUrl = string.Format(
            "{0}{1}/_build/results?buildId={2}&view=logs&j={3}&t={4}",
            ValueProvider.GetValue(PlatformKeys.Ci.Azure.SystemTeamFoundationServerUri),
            ValueProvider.GetValue(PlatformKeys.Ci.Azure.SystemTeamProjectId),
            ValueProvider.GetValue(PlatformKeys.Ci.Azure.BuildBuildId),
            ValueProvider.GetValue(PlatformKeys.Ci.Azure.SystemJobId),
            ValueProvider.GetValue(PlatformKeys.Ci.Azure.SystemTaskInstanceId));

        var prRepo = ValueProvider.GetValue(PlatformKeys.Ci.Azure.SystemPullRequestSourceRepositoryUri);
        Repository = !string.IsNullOrWhiteSpace(prRepo) ? prRepo : ValueProvider.GetValue(PlatformKeys.Ci.Azure.BuildRepositoryUri);

        var prCommit = ValueProvider.GetValue(PlatformKeys.Ci.Azure.SystemPullRequestSourceCommitId);
        Commit = !string.IsNullOrWhiteSpace(prCommit) ? prCommit : ValueProvider.GetValue(PlatformKeys.Ci.Azure.BuildSourceVersion);

        var prBranch = ValueProvider.GetValue(PlatformKeys.Ci.Azure.SystemPullRequestSourceBranch);
        Branch = !string.IsNullOrWhiteSpace(prBranch) ? prBranch : ValueProvider.GetValue(PlatformKeys.Ci.Azure.BuildSourceBranch);

        if (string.IsNullOrWhiteSpace(Branch))
        {
            Branch = ValueProvider.GetValue(PlatformKeys.Ci.Azure.BuildSourceBranchName);
        }

        Message = ValueProvider.GetValue(PlatformKeys.Ci.Azure.BuildSourceVersionMessage);
        AuthorName = ValueProvider.GetValue(PlatformKeys.Ci.Azure.BuildRequestedForId);
        AuthorEmail = ValueProvider.GetValue(PlatformKeys.Ci.Azure.BuildRequestedForEmail);

        VariablesToBypass = new Dictionary<string, string?>();
        SetVariablesIfNotEmpty(
            VariablesToBypass,
            PlatformKeys.Ci.Azure.SystemTeamProjectId,
            PlatformKeys.Ci.Azure.BuildBuildId,
            PlatformKeys.Ci.Azure.SystemJobId);

        PrBaseBranch = ValueProvider.GetValue(PlatformKeys.Ci.Azure.SystemPullRequestTargetBranch);
        PrNumber = ValueProvider.GetValue(PlatformKeys.Ci.Azure.SystemPullRequestNumber);
    }
}
