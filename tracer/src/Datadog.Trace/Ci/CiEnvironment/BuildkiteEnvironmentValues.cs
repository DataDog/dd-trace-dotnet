// <copyright file="BuildkiteEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class BuildkiteEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(GitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Buildkite detected");

        IsCI = true;
        Provider = "buildkite";
        Repository = ValueProvider.GetValue(Constants.BuildKiteRepo);
        Commit = ValueProvider.GetValue(Constants.BuildKiteCommit);
        Branch = ValueProvider.GetValue(Constants.BuildKiteBranch);
        Tag = ValueProvider.GetValue(Constants.BuildKiteTag);
        SourceRoot = ValueProvider.GetValue(Constants.BuildKiteBuildCheckoutPath);
        WorkspacePath = ValueProvider.GetValue(Constants.BuildKiteBuildCheckoutPath);
        PipelineId = ValueProvider.GetValue(Constants.BuildKiteBuildId);
        PipelineNumber = ValueProvider.GetValue(Constants.BuildKiteBuildNumber);
        PipelineName = ValueProvider.GetValue(Constants.BuildKitePipelineSlug);
        PipelineUrl = ValueProvider.GetValue(Constants.BuildKiteBuildUrl);
        JobUrl = string.Format("{0}#{1}", ValueProvider.GetValue(Constants.BuildKiteBuildUrl), ValueProvider.GetValue(Constants.BuildKiteJobId));

        Message = ValueProvider.GetValue(Constants.BuildKiteMessage);
        AuthorName = ValueProvider.GetValue(Constants.BuildKiteBuildAuthor);
        AuthorEmail = ValueProvider.GetValue(Constants.BuildKiteBuildAuthorEmail);
        CommitterName = ValueProvider.GetValue(Constants.BuildKiteBuildCreator);
        CommitterEmail = ValueProvider.GetValue(Constants.BuildKiteBuildCreatorEmail);

        // Node
        NodeName = ValueProvider.GetValue(Constants.BuildKiteAgentId);
        var lstNodeLabels = new List<string>();
        foreach (DictionaryEntry? envvar in ValueProvider.GetValues())
        {
            if (envvar?.Key is string key && key.StartsWith(Constants.BuildKiteAgentMetadata, StringComparison.OrdinalIgnoreCase))
            {
                var name = key.Substring(Constants.BuildKiteAgentMetadata.Length).ToLowerInvariant();
                var value = envvar?.Value?.ToString();
                lstNodeLabels.Add($"{name}:{value}");
            }
        }

        if (lstNodeLabels.Count > 0)
        {
            NodeLabels = lstNodeLabels.ToArray();
        }

        VariablesToBypass = new Dictionary<string, string?>();
        SetVariablesIfNotEmpty(
            VariablesToBypass,
            Constants.BuildKiteBuildId,
            Constants.BuildKiteJobId);
    }
}
