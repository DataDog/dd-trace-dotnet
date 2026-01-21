// <copyright file="TravisEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class TravisEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(IGitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Travis CI detected");

        IsCI = true;
        Provider = "travisci";
        MetricTag = MetricTags.CIVisibilityTestSessionProvider.TravisCi;

        var prSlug = ValueProvider.GetValue(PlatformKeys.Ci.Travis.PullRequestSlug);
        var repoSlug = !string.IsNullOrEmpty(prSlug) ? prSlug : ValueProvider.GetValue(PlatformKeys.Ci.Travis.RepoSlug);

        Repository = $"https://github.com/{repoSlug}.git";
        Commit = ValueProvider.GetValue(PlatformKeys.Ci.Travis.Commit);
        Tag = ValueProvider.GetValue(PlatformKeys.Ci.Travis.Tag);
        if (string.IsNullOrEmpty(Tag))
        {
            Branch = ValueProvider.GetValue(PlatformKeys.Ci.Travis.PullRequestBranch);
            if (string.IsNullOrWhiteSpace(Branch))
            {
                Branch = ValueProvider.GetValue(PlatformKeys.Ci.Travis.Branch);
            }
        }

        SourceRoot = ValueProvider.GetValue(PlatformKeys.Ci.Travis.BuildDir);
        WorkspacePath = ValueProvider.GetValue(PlatformKeys.Ci.Travis.BuildDir);
        PipelineId = ValueProvider.GetValue(PlatformKeys.Ci.Travis.BuildId);
        PipelineNumber = ValueProvider.GetValue(PlatformKeys.Ci.Travis.BuildNumber);
        PipelineName = repoSlug;
        PipelineUrl = ValueProvider.GetValue(PlatformKeys.Ci.Travis.BuildWebUrl);
        JobUrl = ValueProvider.GetValue(PlatformKeys.Ci.Travis.JobWebUrl);

        Message = ValueProvider.GetValue(PlatformKeys.Ci.Travis.CommitMessage);

        PrBaseBranch = ValueProvider.GetValue(PlatformKeys.Ci.Travis.Branch);
        HeadCommit = ValueProvider.GetValue(PlatformKeys.Ci.Travis.PullRequestSha);
        PrNumber = ValueProvider.GetValue(PlatformKeys.Ci.Travis.PullRequestNumber);
    }
}
