// <copyright file="BitbucketEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class BitbucketEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(IGitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Bitbucket detected");

        IsCI = true;
        Provider = "bitbucket";
        MetricTag = MetricTags.CIVisibilityTestSessionProvider.BitBucket;
        Repository = ValueProvider.GetValue(PlatformKeys.Ci.Bitbucket.GitSshOrigin);
        if (string.IsNullOrEmpty(Repository))
        {
            Repository = ValueProvider.GetValue(PlatformKeys.Ci.Bitbucket.GitHttpsOrigin);
        }

        Commit = ValueProvider.GetValue(PlatformKeys.Ci.Bitbucket.Commit);
        Branch = ValueProvider.GetValue(PlatformKeys.Ci.Bitbucket.Branch);
        Tag = ValueProvider.GetValue(PlatformKeys.Ci.Bitbucket.Tag);
        SourceRoot = ValueProvider.GetValue(PlatformKeys.Ci.Bitbucket.CloneDir);
        WorkspacePath = ValueProvider.GetValue(PlatformKeys.Ci.Bitbucket.CloneDir);
        PipelineId = ValueProvider.GetValue(PlatformKeys.Ci.Bitbucket.PipelineUuid)?.Replace("}", string.Empty).Replace("{", string.Empty);
        PipelineNumber = ValueProvider.GetValue(PlatformKeys.Ci.Bitbucket.BuildNumber);
        PipelineName = ValueProvider.GetValue(PlatformKeys.Ci.Bitbucket.RepoFullName);
        PipelineUrl = string.Format(
            "https://bitbucket.org/{0}/addon/pipelines/home#!/results/{1}",
            ValueProvider.GetValue(PlatformKeys.Ci.Bitbucket.RepoFullName),
            ValueProvider.GetValue(PlatformKeys.Ci.Bitbucket.BuildNumber));
        JobUrl = PipelineUrl;

        PrBaseBranch = ValueProvider.GetValue(PlatformKeys.Ci.Bitbucket.PullRequestDestinationBranch);
        PrNumber = ValueProvider.GetValue(PlatformKeys.Ci.Bitbucket.PullRequestNumber);
    }
}
