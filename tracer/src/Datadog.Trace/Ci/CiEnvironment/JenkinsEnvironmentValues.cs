// <copyright file="JenkinsEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class JenkinsEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(IGitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Jenkins detected");

        IsCI = true;
        Provider = "jenkins";
        MetricTag = MetricTags.CIVisibilityTestSessionProvider.Jenkins;
        Repository = ValueProvider.GetValue(PlatformKeys.Ci.Jenkins.GitUrl);
        if (string.IsNullOrEmpty(Repository))
        {
            Repository = ValueProvider.GetValue(PlatformKeys.Ci.Jenkins.GitUrl1);
        }

        Commit = ValueProvider.GetValue(PlatformKeys.Ci.Jenkins.GitCommit);

        var gitBranch = ValueProvider.GetValue(PlatformKeys.Ci.Jenkins.GitBranch);
        if (gitBranch?.Contains("tags") == true)
        {
            Tag = gitBranch;
        }
        else
        {
            Branch = gitBranch;
        }

        SourceRoot = ValueProvider.GetValue(PlatformKeys.Ci.Jenkins.Workspace);
        WorkspacePath = ValueProvider.GetValue(PlatformKeys.Ci.Jenkins.Workspace);
        PipelineId = ValueProvider.GetValue(PlatformKeys.Ci.Jenkins.BuildTag);
        PipelineNumber = ValueProvider.GetValue(PlatformKeys.Ci.Jenkins.BuildNumber);
        PipelineUrl = ValueProvider.GetValue(PlatformKeys.Ci.Jenkins.BuildUrl);

        // Pipeline Name algorithm from: https://github.com/DataDog/dd-trace-java/blob/master/internal-api/src/main/java/datadog/trace/bootstrap/instrumentation/api/ci/JenkinsInfo.java
        var pipelineName = ValueProvider.GetValue(PlatformKeys.Ci.Jenkins.JobName);
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
        NodeName = ValueProvider.GetValue(PlatformKeys.Ci.Jenkins.NodeName);
        NodeLabels = ValueProvider.GetValue(PlatformKeys.Ci.Jenkins.NodeLabels)?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        VariablesToBypass = new Dictionary<string, string?>();
        SetVariablesIfNotEmpty(
            VariablesToBypass,
            ConfigurationKeys.CIVisibility.CustomTraceId);

        PrBaseBranch = ValueProvider.GetValue(PlatformKeys.Ci.Jenkins.ChangeTarget);
        PrNumber = ValueProvider.GetValue(PlatformKeys.Ci.Jenkins.ChangeId);
    }
}
