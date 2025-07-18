// <copyright file="AWSCodePipelineEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class AWSCodePipelineEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(IGitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: AWS CodePipeline detected");

        IsCI = true;
        Provider = "awscodepipeline";
        MetricTag = MetricTags.CIVisibilityTestSessionProvider.AwsCodePipeline;
        PipelineId = ValueProvider.GetValue(Constants.AWSCodePipelineId);
        JobId = ValueProvider.GetValue(Constants.AWSCodePipelineActionExecutionId);

        VariablesToBypass = new Dictionary<string, string?>();
        SetVariablesIfNotEmpty(
            VariablesToBypass,
            Constants.AWSCodePipelineBuildArn,
            Constants.AWSCodePipelineId,
            Constants.AWSCodePipelineActionExecutionId);
    }
}
