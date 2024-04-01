// <copyright file="BitriseEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class BitriseEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(GitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Bitrise detected");

        IsCI = true;
        Provider = "bitrise";
        Repository = ValueProvider.GetValue(Constants.BitriseGitRepositoryUrl);

        var prCommit = ValueProvider.GetValue(Constants.BitriseGitCommit);
        Commit = !string.IsNullOrWhiteSpace(prCommit) ? prCommit : ValueProvider.GetValue(Constants.BitriseGitCloneCommitHash);

        var prBranch = ValueProvider.GetValue(Constants.BitriseGitBranchDest);
        Branch = !string.IsNullOrWhiteSpace(prBranch) ? prBranch : ValueProvider.GetValue(Constants.BitriseGitBranch);

        Tag = ValueProvider.GetValue(Constants.BitriseGitTag);
        SourceRoot = ValueProvider.GetValue(Constants.BitriseSourceDir);
        WorkspacePath = ValueProvider.GetValue(Constants.BitriseSourceDir);
        PipelineId = ValueProvider.GetValue(Constants.BitriseBuildSlug);
        PipelineNumber = ValueProvider.GetValue(Constants.BitriseBuildNumber);
        PipelineName = ValueProvider.GetValue(Constants.BitriseTriggeredWorkflowId);
        PipelineUrl = ValueProvider.GetValue(Constants.BitriseBuildUrl);

        Message = ValueProvider.GetValue(Constants.BitriseGitMessage);
        AuthorName = ValueProvider.GetValue(Constants.BitriseCloneCommitAuthorName);
        AuthorEmail = ValueProvider.GetValue(Constants.BitriseCloneCommitAuthorEmail);
        CommitterName = ValueProvider.GetValue(Constants.BitriseCloneCommitCommiterName);
        CommitterEmail = ValueProvider.GetValue(Constants.BitriseCloneCommitCommiterEmail);
        if (string.IsNullOrWhiteSpace(CommitterEmail))
        {
            CommitterEmail = CommitterName;
        }
    }
}
