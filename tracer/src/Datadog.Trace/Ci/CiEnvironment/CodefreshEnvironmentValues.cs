// <copyright file="CodefreshEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class CodefreshEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(IGitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Codefresh detected");

        IsCI = true;
        Provider = "codefresh";
        MetricTag = MetricTags.CIVisibilityTestSessionProvider.Codefresh;
        PipelineId = ValueProvider.GetValue(PlatformKeys.Ci.Codefresh.BuildId);
        PipelineName = ValueProvider.GetValue(PlatformKeys.Ci.Codefresh.PipelineName);
        PipelineUrl = ValueProvider.GetValue(PlatformKeys.Ci.Codefresh.BuildUrl);
        JobName = ValueProvider.GetValue(PlatformKeys.Ci.Codefresh.StepName);
        Branch = ValueProvider.GetValue(PlatformKeys.Ci.Codefresh.Branch) ?? gitInfo.Branch;

        Commit = gitInfo.Commit;
        Repository = gitInfo.Repository;
        Message = gitInfo.Message;
        AuthorName = gitInfo.AuthorName;
        AuthorEmail = gitInfo.AuthorEmail;
        AuthorDate = gitInfo.AuthorDate;
        CommitterName = gitInfo.CommitterName;
        CommitterEmail = gitInfo.CommitterEmail;
        CommitterDate = gitInfo.CommitterDate;
        SourceRoot = gitInfo.SourceRoot;
        WorkspacePath = gitInfo.SourceRoot;

        VariablesToBypass = new Dictionary<string, string?>();
        SetVariablesIfNotEmpty(
            VariablesToBypass,
            PlatformKeys.Ci.Codefresh.BuildId);

        PrBaseBranch = ValueProvider.GetValue(PlatformKeys.Ci.Codefresh.PullRequestTarget);
        PrNumber = ValueProvider.GetValue(PlatformKeys.Ci.Codefresh.PullRequestNumber);
    }
}
