// <copyright file="AzurePipelinesEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class AzurePipelinesEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(GitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Azure Pipelines detected");

        IsCI = true;
        Provider = "azurepipelines";
        SourceRoot = ValueProvider.GetValue(Constants.AzureBuildSourcesDirectory);
        WorkspacePath = ValueProvider.GetValue(Constants.AzureBuildSourcesDirectory);
        PipelineId = ValueProvider.GetValue(Constants.AzureBuildBuildId);
        PipelineName = ValueProvider.GetValue(Constants.AzureBuildDefinitionName);
        PipelineNumber = ValueProvider.GetValue(Constants.AzureBuildBuildId);
        PipelineUrl = string.Format(
            "{0}{1}/_build/results?buildId={2}",
            ValueProvider.GetValue(Constants.AzureSystemTeamFoundationServerUri),
            ValueProvider.GetValue(Constants.AzureSystemTeamProjectId),
            ValueProvider.GetValue(Constants.AzureBuildBuildId));

        StageName = ValueProvider.GetValue(Constants.AzureSystemStageDisplayName);

        JobName = ValueProvider.GetValue(Constants.AzureSystemJobDisplayName);
        JobUrl = string.Format(
            "{0}{1}/_build/results?buildId={2}&view=logs&j={3}&t={4}",
            ValueProvider.GetValue(Constants.AzureSystemTeamFoundationServerUri),
            ValueProvider.GetValue(Constants.AzureSystemTeamProjectId),
            ValueProvider.GetValue(Constants.AzureBuildBuildId),
            ValueProvider.GetValue(Constants.AzureSystemJobId),
            ValueProvider.GetValue(Constants.AzureSystemTaskInstanceId));

        var prRepo = ValueProvider.GetValue(Constants.AzureSystemPullRequestSourceRepositoryUri);
        Repository = !string.IsNullOrWhiteSpace(prRepo) ? prRepo : ValueProvider.GetValue(Constants.AzureBuildRepositoryUri);

        var prCommit = ValueProvider.GetValue(Constants.AzureSystemPullRequestSourceCommitId);
        Commit = !string.IsNullOrWhiteSpace(prCommit) ? prCommit : ValueProvider.GetValue(Constants.AzureBuildSourceVersion);

        var prBranch = ValueProvider.GetValue(Constants.AzureSystemPullRequestSourceBranch);
        Branch = !string.IsNullOrWhiteSpace(prBranch) ? prBranch : ValueProvider.GetValue(Constants.AzureBuildSourceBranch);

        if (string.IsNullOrWhiteSpace(Branch))
        {
            Branch = ValueProvider.GetValue(Constants.AzureBuildSourceBranchName);
        }

        Message = ValueProvider.GetValue(Constants.AzureBuildSourceVersionMessage);
        AuthorName = ValueProvider.GetValue(Constants.AzureBuildRequestedForId);
        AuthorEmail = ValueProvider.GetValue(Constants.AzureBuildRequestedForEmail);

        VariablesToBypass = new Dictionary<string, string?>();
        SetVariablesIfNotEmpty(
            VariablesToBypass,
            Constants.AzureSystemTeamProjectId,
            Constants.AzureBuildBuildId,
            Constants.AzureSystemJobId);
    }
}
