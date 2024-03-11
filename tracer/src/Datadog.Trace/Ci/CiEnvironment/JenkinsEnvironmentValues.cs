// <copyright file="JenkinsEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class JenkinsEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(GitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Jenkins detected");

        IsCI = true;
        Provider = "jenkins";
        Repository = ValueProvider.GetValue(Constants.JenkinsGitUrl);
        if (string.IsNullOrEmpty(Repository))
        {
            Repository = ValueProvider.GetValue(Constants.JenkinsGitUrl1);
        }

        Commit = ValueProvider.GetValue(Constants.JenkinsGitCommit);

        var gitBranch = ValueProvider.GetValue(Constants.JenkinsGitBranch);
        if (gitBranch?.Contains("tags") == true)
        {
            Tag = gitBranch;
        }
        else
        {
            Branch = gitBranch;
        }

        SourceRoot = ValueProvider.GetValue(Constants.JenkinsWorkspace);
        WorkspacePath = ValueProvider.GetValue(Constants.JenkinsWorkspace);
        PipelineId = ValueProvider.GetValue(Constants.JenkinsBuildTag);
        PipelineNumber = ValueProvider.GetValue(Constants.JenkinsBuildNumber);
        PipelineUrl = ValueProvider.GetValue(Constants.JenkinsBuildUrl);

        // Pipeline Name algorithm from: https://github.com/DataDog/dd-trace-java/blob/master/internal-api/src/main/java/datadog/trace/bootstrap/instrumentation/api/ci/JenkinsInfo.java
        var pipelineName = ValueProvider.GetValue(Constants.JenkinsJobName);
        if (pipelineName != null)
        {
            CleanBranchAndTag();

            // First, the git branch is removed from the raw jobName
            var jobNameNoBranch = Branch != null ? pipelineName.Trim().Replace("/" + Branch, string.Empty) : pipelineName;

            // Once the branch has been removed, we try to extract
            // the configurations from the job name.
            // The configurations have the form like "key1=value1,key2=value2"
            var configurations = new Dictionary<string, string>();
            var jobNameParts = jobNameNoBranch.Split('/');
            if (jobNameParts.Length > 1 && jobNameParts[1].Contains("="))
            {
                var configsStr = jobNameParts[1].ToLowerInvariant().Trim();
                var configsKeyValue = configsStr.Split(',');
                foreach (var configKeyValue in configsKeyValue)
                {
                    var keyValue = configKeyValue.Trim().Split('=');
                    configurations[keyValue[0]] = keyValue[1];
                }
            }

            if (configurations.Count == 0)
            {
                // If there is no configurations,
                // the jobName is the original one without branch.
                PipelineName = jobNameNoBranch;
            }
            else
            {
                // If there are configurations,
                // the jobName is the first part of the split raw jobName.
                PipelineName = jobNameParts[0];
            }
        }

        // Node
        NodeName = ValueProvider.GetValue(Constants.JenkinsNodeName);
        NodeLabels = ValueProvider.GetValue(Constants.JenkinsNodeLabels)?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        VariablesToBypass = new Dictionary<string, string?>();
        SetVariablesIfNotEmpty(
            VariablesToBypass,
            Constants.JenkinsCustomTraceId);
    }
}
