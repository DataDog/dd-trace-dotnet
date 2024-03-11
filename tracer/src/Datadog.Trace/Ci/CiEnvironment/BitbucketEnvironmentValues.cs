// <copyright file="BitbucketEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class BitbucketEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(GitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Bitbucket detected");

        IsCI = true;
        Provider = "bitbucket";
        Repository = ValueProvider.GetValue(Constants.BitBucketGitSshOrigin);
        if (string.IsNullOrEmpty(Repository))
        {
            Repository = ValueProvider.GetValue(Constants.BitBucketGitHttpsOrigin);
        }

        Commit = ValueProvider.GetValue(Constants.BitBucketCommit);
        Branch = ValueProvider.GetValue(Constants.BitBucketBranch);
        Tag = ValueProvider.GetValue(Constants.BitBucketTag);
        SourceRoot = ValueProvider.GetValue(Constants.BitBucketCloneDir);
        WorkspacePath = ValueProvider.GetValue(Constants.BitBucketCloneDir);
        PipelineId = ValueProvider.GetValue(Constants.BitBucketPipelineUuid)?.Replace("}", string.Empty).Replace("{", string.Empty);
        PipelineNumber = ValueProvider.GetValue(Constants.BitBucketBuildNumber);
        PipelineName = ValueProvider.GetValue(Constants.BitBucketRepoFullName);
        PipelineUrl = string.Format(
            "https://bitbucket.org/{0}/addon/pipelines/home#!/results/{1}",
            ValueProvider.GetValue(Constants.BitBucketRepoFullName),
            ValueProvider.GetValue(Constants.BitBucketBuildNumber));
        JobUrl = PipelineUrl;
    }
}
