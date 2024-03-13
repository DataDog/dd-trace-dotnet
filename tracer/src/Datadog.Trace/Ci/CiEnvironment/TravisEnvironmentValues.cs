// <copyright file="TravisEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class TravisEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(GitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Travis CI detected");

        IsCI = true;
        Provider = "travisci";

        var prSlug = ValueProvider.GetValue(Constants.TravisPullRequestSlug);
        var repoSlug = !string.IsNullOrEmpty(prSlug) ? prSlug : ValueProvider.GetValue(Constants.TravisRepoSlug);

        Repository = $"https://github.com/{repoSlug}.git";
        Commit = ValueProvider.GetValue(Constants.TravisCommit);
        Tag = ValueProvider.GetValue(Constants.TravisTag);
        if (string.IsNullOrEmpty(Tag))
        {
            Branch = ValueProvider.GetValue(Constants.TravisPullRequestBranch);
            if (string.IsNullOrWhiteSpace(Branch))
            {
                Branch = ValueProvider.GetValue(Constants.TravisBranch);
            }
        }

        SourceRoot = ValueProvider.GetValue(Constants.TravisBuildDir);
        WorkspacePath = ValueProvider.GetValue(Constants.TravisBuildDir);
        PipelineId = ValueProvider.GetValue(Constants.TravisBuildId);
        PipelineNumber = ValueProvider.GetValue(Constants.TravisBuildNumber);
        PipelineName = repoSlug;
        PipelineUrl = ValueProvider.GetValue(Constants.TravisBuildWebUrl);
        JobUrl = ValueProvider.GetValue(Constants.TravisJobWebUrl);

        Message = ValueProvider.GetValue(Constants.TravisCommitMessage);
    }
}
