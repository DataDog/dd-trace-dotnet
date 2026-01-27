// <copyright file="BuddyEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class BuddyEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(IGitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Buddy detected");

        IsCI = true;
        Provider = "buddy";
        MetricTag = MetricTags.CIVisibilityTestSessionProvider.BuddyCi;
        Repository = ValueProvider.GetValue(PlatformKeys.Ci.Buddy.ScmUrl);
        Commit = ValueProvider.GetValue(PlatformKeys.Ci.Buddy.ExecutionRevision);
        Branch = ValueProvider.GetValue(PlatformKeys.Ci.Buddy.ExecutionBranch);
        Tag = ValueProvider.GetValue(PlatformKeys.Ci.Buddy.ExecutionTag);

        PipelineId = string.Format(
            "{0}/{1}",
            ValueProvider.GetValue(PlatformKeys.Ci.Buddy.PipelineId),
            ValueProvider.GetValue(PlatformKeys.Ci.Buddy.ExecutionId));
        PipelineName = ValueProvider.GetValue(PlatformKeys.Ci.Buddy.PipelineName);
        PipelineNumber = ValueProvider.GetValue(PlatformKeys.Ci.Buddy.ExecutionId);
        PipelineUrl = ValueProvider.GetValue(PlatformKeys.Ci.Buddy.ExecutionUrl);

        Message = ValueProvider.GetValue(PlatformKeys.Ci.Buddy.ExecutionRevisionMessage);
        CommitterName = ValueProvider.GetValue(PlatformKeys.Ci.Buddy.ExecutionRevisionCommitterName);
        CommitterEmail = ValueProvider.GetValue(PlatformKeys.Ci.Buddy.ExecutionRevisionCommitterEmail);
        if (string.IsNullOrWhiteSpace(CommitterEmail))
        {
            CommitterEmail = CommitterName;
        }

        SourceRoot = gitInfo.SourceRoot;
        WorkspacePath = gitInfo.SourceRoot;

        PrBaseBranch = ValueProvider.GetValue(PlatformKeys.Ci.Buddy.PullRequestBaseBranch);
        PrNumber = ValueProvider.GetValue(PlatformKeys.Ci.Buddy.PullRequestNumber);
    }
}
