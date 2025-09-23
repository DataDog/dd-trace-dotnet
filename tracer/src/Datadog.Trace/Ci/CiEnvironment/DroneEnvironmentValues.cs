// <copyright file="DroneEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.Collections.Generic;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class DroneEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(IGitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: Drone detected");

        IsCI = true;
        Provider = "drone";
        MetricTag = MetricTags.CIVisibilityTestSessionProvider.Drone;
        Branch = ValueProvider.GetValue(Constants.DroneBranch);
        PipelineUrl = ValueProvider.GetValue(Constants.DroneBuildLink);
        PipelineNumber = ValueProvider.GetValue(Constants.DroneBuildNumber);
        AuthorEmail = ValueProvider.GetValue(Constants.DroneCommitAuthorEmail);
        AuthorName = ValueProvider.GetValue(Constants.DroneCommitAuthorName);
        Message = ValueProvider.GetValue(Constants.DroneCommitMessage);
        Commit = ValueProvider.GetValue(Constants.DroneCommitSha);
        Repository = ValueProvider.GetValue(Constants.DroneGitHttpUrl);
        StageName = ValueProvider.GetValue(Constants.DroneStageName);
        JobName = ValueProvider.GetValue(Constants.DroneStepName);
        Tag = ValueProvider.GetValue(Constants.DroneTag);
        WorkspacePath = ValueProvider.GetValue(Constants.DroneWorkspace);
        PrNumber = ValueProvider.GetValue(Constants.DronePullRequest);
        PrBaseBranch = ValueProvider.GetValue(Constants.DroneTargetBranch);
    }
}
