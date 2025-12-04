// <copyright file="CircleCiEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class CircleCiEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(IGitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: CircleCI detected");

        IsCI = true;
        Provider = "circleci";
        MetricTag = MetricTags.CIVisibilityTestSessionProvider.CircleCI;
        Repository = ValueProvider.GetValue(PlatformKeys.Ci.CircleCI.RepositoryUrl);
        Commit = ValueProvider.GetValue(PlatformKeys.Ci.CircleCI.Sha);
        Tag = ValueProvider.GetValue(PlatformKeys.Ci.CircleCI.Tag);
        if (string.IsNullOrEmpty(Tag))
        {
            Branch = ValueProvider.GetValue(PlatformKeys.Ci.CircleCI.Branch);
        }

        SourceRoot = ValueProvider.GetValue(PlatformKeys.Ci.CircleCI.WorkingDirectory);
        WorkspacePath = ValueProvider.GetValue(PlatformKeys.Ci.CircleCI.WorkingDirectory);
        PipelineId = ValueProvider.GetValue(PlatformKeys.Ci.CircleCI.WorkflowId);
        PipelineName = ValueProvider.GetValue(PlatformKeys.Ci.CircleCI.ProjectRepoName);
        PipelineUrl = $"https://app.circleci.com/pipelines/workflows/{PipelineId}";
        JobName = ValueProvider.GetValue(PlatformKeys.Ci.CircleCI.Job);
        JobId = ValueProvider.GetValue(PlatformKeys.Ci.CircleCI.BuildNum);
        JobUrl = ValueProvider.GetValue(PlatformKeys.Ci.CircleCI.BuildUrl);

        VariablesToBypass = new Dictionary<string, string?>();
        SetVariablesIfNotEmpty(
            VariablesToBypass,
            PlatformKeys.Ci.CircleCI.WorkflowId,
            PlatformKeys.Ci.CircleCI.BuildNum);

        PrNumber = ValueProvider.GetValue(PlatformKeys.Ci.CircleCI.PrNumber);
    }
}
