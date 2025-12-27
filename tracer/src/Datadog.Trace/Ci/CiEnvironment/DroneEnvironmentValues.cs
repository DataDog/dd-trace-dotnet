// <copyright file="DroneEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.Collections.Generic;
using Datadog.Trace.Configuration;
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
        Branch = ValueProvider.GetValue(PlatformKeys.Ci.Drone.Branch);
        PipelineUrl = ValueProvider.GetValue(PlatformKeys.Ci.Drone.BuildLink);
        PipelineNumber = ValueProvider.GetValue(PlatformKeys.Ci.Drone.BuildNumber);
        AuthorEmail = ValueProvider.GetValue(PlatformKeys.Ci.Drone.CommitAuthorEmail);
        AuthorName = ValueProvider.GetValue(PlatformKeys.Ci.Drone.CommitAuthorName);
        Message = ValueProvider.GetValue(PlatformKeys.Ci.Drone.CommitMessage);
        Commit = ValueProvider.GetValue(PlatformKeys.Ci.Drone.CommitSha);
        Repository = ValueProvider.GetValue(PlatformKeys.Ci.Drone.GitHttpUrl);
        StageName = ValueProvider.GetValue(PlatformKeys.Ci.Drone.StageName);
        JobName = ValueProvider.GetValue(PlatformKeys.Ci.Drone.StepName);
        Tag = ValueProvider.GetValue(PlatformKeys.Ci.Drone.Tag);
        WorkspacePath = ValueProvider.GetValue(PlatformKeys.Ci.Drone.Workspace);
        PrNumber = ValueProvider.GetValue(PlatformKeys.Ci.Drone.PullRequest);
        PrBaseBranch = ValueProvider.GetValue(PlatformKeys.Ci.Drone.TargetBranch);
    }
}
