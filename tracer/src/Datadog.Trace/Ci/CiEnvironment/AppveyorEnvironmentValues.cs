// <copyright file="AppveyorEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class AppveyorEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(GitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Appveyor detected");

        IsCI = true;
        Provider = "appveyor";
        var repoProvider = ValueProvider.GetValue(Constants.AppveyorRepoProvider);
        if (repoProvider == "github")
        {
            Repository = string.Format("https://github.com/{0}.git", ValueProvider.GetValue(Constants.AppveyorRepoName));
        }
        else
        {
            Repository = ValueProvider.GetValue(Constants.AppveyorRepoName);
        }

        Commit = ValueProvider.GetValue(Constants.AppveyorRepoCommit);
        SourceRoot = ValueProvider.GetValue(Constants.AppveyorBuildFolder);
        WorkspacePath = ValueProvider.GetValue(Constants.AppveyorBuildFolder);
        PipelineId = ValueProvider.GetValue(Constants.AppveyorBuildId);
        PipelineName = ValueProvider.GetValue(Constants.AppveyorRepoName);
        PipelineNumber = ValueProvider.GetValue(Constants.AppveyorBuildNumber);
        PipelineUrl = string.Format("https://ci.appveyor.com/project/{0}/builds/{1}", ValueProvider.GetValue(Constants.AppveyorRepoName), ValueProvider.GetValue(Constants.AppveyorBuildId));
        JobUrl = PipelineUrl;
        Branch = ValueProvider.GetValue(Constants.AppveyorPullRequestHeadRepoBranch);
        Tag = ValueProvider.GetValue(Constants.AppveyorRepoTagName);
        if (string.IsNullOrWhiteSpace(Branch))
        {
            Branch = ValueProvider.GetValue(Constants.AppveyorRepoBranch);
        }

        Message = ValueProvider.GetValue(Constants.AppveyorRepoCommitMessage);
        var extendedMessage = ValueProvider.GetValue(Constants.AppveyorRepoCommitMessageExtended);
        if (!string.IsNullOrWhiteSpace(extendedMessage))
        {
            Message = Message + "\n" + extendedMessage;
        }

        AuthorName = ValueProvider.GetValue(Constants.AppveyorRepoCommitAuthor);
        AuthorEmail = ValueProvider.GetValue(Constants.AppveyorRepoCommitAuthorEmail);
    }
}
