// <copyright file="CircleCiEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class CircleCiEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(GitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: CircleCI detected");

        IsCI = true;
        Provider = "circleci";
        Repository = ValueProvider.GetValue(Constants.CircleCIRepositoryUrl);
        Commit = ValueProvider.GetValue(Constants.CircleCISha);
        Tag = ValueProvider.GetValue(Constants.CircleCITag);
        if (string.IsNullOrEmpty(Tag))
        {
            Branch = ValueProvider.GetValue(Constants.CircleCIBranch);
        }

        SourceRoot = ValueProvider.GetValue(Constants.CircleCIWorkingDirectory);
        WorkspacePath = ValueProvider.GetValue(Constants.CircleCIWorkingDirectory);
        PipelineId = ValueProvider.GetValue(Constants.CircleCIWorkflowId);
        PipelineName = ValueProvider.GetValue(Constants.CircleCIProjectRepoName);
        PipelineUrl = $"https://app.circleci.com/pipelines/workflows/{PipelineId}";
        JobName = ValueProvider.GetValue(Constants.CircleCIJob);
        JobUrl = ValueProvider.GetValue(Constants.CircleCIBuildUrl);

        VariablesToBypass = new Dictionary<string, string?>();
        SetVariablesIfNotEmpty(
            VariablesToBypass,
            Constants.CircleCIWorkflowId,
            Constants.CircleCIBuildNum);
    }
}
