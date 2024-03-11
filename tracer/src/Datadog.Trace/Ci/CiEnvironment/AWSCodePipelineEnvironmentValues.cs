// <copyright file="AWSCodePipelineEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class AWSCodePipelineEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(GitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: AWS CodePipeline detected");

        IsCI = true;
        Provider = "awscodepipeline";
        PipelineId = ValueProvider.GetValue(Constants.AWSCodePipelineId);

        VariablesToBypass = new Dictionary<string, string?>();
        SetVariablesIfNotEmpty(
            VariablesToBypass,
            Constants.AWSCodePipelineBuildArn,
            Constants.AWSCodePipelineId,
            Constants.AWSCodePipelineActionExecutionId);
    }
}
