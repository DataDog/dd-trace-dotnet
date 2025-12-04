// <copyright file="AppveyorEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class AppveyorEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(IGitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Appveyor detected");

        IsCI = true;
        Provider = "appveyor";
        MetricTag = MetricTags.CIVisibilityTestSessionProvider.AppVeyor;
        var repoProvider = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.RepoProvider);
        if (repoProvider == "github")
        {
            Repository = string.Format("https://github.com/{0}.git", ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.RepoName));
        }
        else
        {
            Repository = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.RepoName);
        }

        Commit = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.RepoCommit);
        SourceRoot = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.BuildFolder);
        WorkspacePath = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.BuildFolder);
        PipelineId = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.BuildId);
        PipelineName = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.RepoName);
        PipelineNumber = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.BuildNumber);
        PipelineUrl = string.Format("https://ci.appveyor.com/project/{0}/builds/{1}", ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.RepoName), ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.BuildId));
        JobUrl = PipelineUrl;
        Branch = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.PullRequestHeadRepoBranch);
        Tag = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.RepoTagName);
        if (string.IsNullOrWhiteSpace(Branch))
        {
            Branch = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.RepoBranch);
        }

        Message = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.RepoCommitMessage);
        var extendedMessage = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.RepoCommitMessageExtended);
        if (!string.IsNullOrWhiteSpace(extendedMessage))
        {
            Message = Message + "\n" + extendedMessage;
        }

        AuthorName = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.RepoCommitAuthor);
        AuthorEmail = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.RepoCommitAuthorEmail);

        PrBaseBranch = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.PullRequestBaseRepoBranch);
        HeadCommit = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.PullRequestHeadCommit);
        PrNumber = ValueProvider.GetValue(PlatformKeys.Ci.AppVeyor.PullRequestNumber);
    }
}
