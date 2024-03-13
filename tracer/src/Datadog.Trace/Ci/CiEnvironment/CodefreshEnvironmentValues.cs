// <copyright file="CodefreshEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class CodefreshEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(GitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Codefresh detected");

        IsCI = true;
        Provider = "codefresh";
        PipelineId = ValueProvider.GetValue(Constants.CodefreshBuildId);
        PipelineName = ValueProvider.GetValue(Constants.CodefreshPipelineName);
        PipelineUrl = ValueProvider.GetValue(Constants.CodefreshBuildUrl);
        JobName = ValueProvider.GetValue(Constants.CodefreshStepName);
        Branch = ValueProvider.GetValue(Constants.CodefreshBranch) ?? gitInfo.Branch;

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
            Constants.CodefreshBuildId);
    }
}
