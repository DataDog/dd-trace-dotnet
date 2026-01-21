// <copyright file="BitriseEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class BitriseEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(IGitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Bitrise detected");

        IsCI = true;
        Provider = "bitrise";
        MetricTag = MetricTags.CIVisibilityTestSessionProvider.Bitrise;
        Repository = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.GitRepositoryUrl);

        var prCommit = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.GitCommit);
        Commit = !string.IsNullOrWhiteSpace(prCommit) ? prCommit : ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.GitCloneCommitHash);

        var prBranch = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.PullRequestHeadBranch);
        Branch = !string.IsNullOrWhiteSpace(prBranch) ? prBranch : ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.GitBranch);

        Tag = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.GitTag);
        SourceRoot = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.SourceDir);
        WorkspacePath = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.SourceDir);
        PipelineId = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.BuildSlug);
        PipelineNumber = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.BuildNumber);
        PipelineName = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.TriggeredWorkflowId);
        PipelineUrl = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.BuildUrl);

        Message = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.GitMessage);
        AuthorName = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.CloneCommitAuthorName);
        AuthorEmail = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.CloneCommitAuthorEmail);
        CommitterName = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.CloneCommitCommiterName);
        CommitterEmail = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.CloneCommitCommiterEmail);
        if (string.IsNullOrWhiteSpace(CommitterEmail))
        {
            CommitterEmail = CommitterName;
        }

        PrBaseBranch = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.GitBranchDest);
        PrNumber = ValueProvider.GetValue(PlatformKeys.Ci.Bitrise.PullRequestNumber);
    }
}
