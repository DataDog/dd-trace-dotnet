// <copyright file="BuddyEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class BuddyEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(GitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Buddy detected");

        IsCI = true;
        Provider = "buddy";
        Repository = ValueProvider.GetValue(Constants.BuddyScmUrl);
        Commit = ValueProvider.GetValue(Constants.BuddyExecutionRevision);
        Branch = ValueProvider.GetValue(Constants.BuddyExecutionBranch);
        Tag = ValueProvider.GetValue(Constants.BuddyExecutionTag);

        PipelineId = string.Format(
            "{0}/{1}",
            ValueProvider.GetValue(Constants.BuddyPipelineId),
            ValueProvider.GetValue(Constants.BuddyExecutionId));
        PipelineName = ValueProvider.GetValue(Constants.BuddyPipelineName);
        PipelineNumber = ValueProvider.GetValue(Constants.BuddyExecutionId);
        PipelineUrl = ValueProvider.GetValue(Constants.BuddyExecutionUrl);

        Message = ValueProvider.GetValue(Constants.BuddyExecutionRevisionMessage);
        CommitterName = ValueProvider.GetValue(Constants.BuddyExecutionRevisionCommitterName);
        CommitterEmail = ValueProvider.GetValue(Constants.BuddyExecutionRevisionCommitterEmail);
        if (string.IsNullOrWhiteSpace(CommitterEmail))
        {
            CommitterEmail = CommitterName;
        }

        SourceRoot = gitInfo.SourceRoot;
        WorkspacePath = gitInfo.SourceRoot;
    }
}
