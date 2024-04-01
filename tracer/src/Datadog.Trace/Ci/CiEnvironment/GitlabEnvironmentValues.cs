// <copyright file="GitlabEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class GitlabEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(GitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Gitlab CI detected");

        IsCI = true;
        Provider = "gitlab";
        Repository = ValueProvider.GetValue(Constants.GitlabRepositoryUrl);
        Commit = ValueProvider.GetValue(Constants.GitlabCommitSha);
        Branch = ValueProvider.GetValue(Constants.GitlabCommitBranch);
        Tag = ValueProvider.GetValue(Constants.GitlabCommitTag);
        if (string.IsNullOrWhiteSpace(Branch))
        {
            Branch = ValueProvider.GetValue(Constants.GitlabCommitRefName);
        }

        SourceRoot = ValueProvider.GetValue(Constants.GitlabProjectDir);
        WorkspacePath = ValueProvider.GetValue(Constants.GitlabProjectDir);

        PipelineId = ValueProvider.GetValue(Constants.GitlabPipelineId);
        PipelineName = ValueProvider.GetValue(Constants.GitlabProjectPath);
        PipelineNumber = ValueProvider.GetValue(Constants.GitlabPipelineIId);
        PipelineUrl = ValueProvider.GetValue(Constants.GitlabPipelineUrl);

        JobUrl = ValueProvider.GetValue(Constants.GitlabJobUrl);
        JobName = ValueProvider.GetValue(Constants.GitlabJobName);
        StageName = ValueProvider.GetValue(Constants.GitlabJobStage);

        Message = ValueProvider.GetValue(Constants.GitlabCommitMessage);

        var author = ValueProvider.GetValue(Constants.GitlabCommitAuthor);
        var authorArray = author?.Split('<', '>');
        AuthorName = authorArray?[0].Trim();
        AuthorEmail = authorArray?[1].Trim();

        var authorDate = GetDateTimeOffsetVariableIfIsNotEmpty(Constants.GitlabCommitTimestamp, null);
        if (authorDate is not null)
        {
            AuthorDate = authorDate;
        }

        // Node
        NodeName = ValueProvider.GetValue(Constants.GitlabRunnerId);
        if (ValueProvider.GetValue(Constants.GitlabRunnerTags) is { } runnerTags)
        {
            try
            {
                NodeLabels = Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(runnerTags);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error deserializing '{GitlabRunnerTags}' environment variable.", Constants.GitlabRunnerTags);
            }
        }

        VariablesToBypass = new Dictionary<string, string?>();
        SetVariablesIfNotEmpty(
            VariablesToBypass,
            Constants.GitlabProjectUrl,
            Constants.GitlabPipelineId,
            Constants.GitlabJobId);
    }
}
