// <copyright file="StartExecutionAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.StepFunctions;

/// <summary>
/// AWSSDK.StepFunctions StartExecutionAsync CallTarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.StepFunctions",
    TypeName = "Amazon.StepFunctions.AmazonStepFunctionsClient",
    MethodName = "StartExecutionAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.StepFunctions.Model.StartExecutionResponse]",
    ParameterTypeNames = ["Amazon.StepFunctions.Model.StartExecutionRequest", ClrNames.CancellationToken],
    MinimumVersion = "3.3.0",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(IntegrationId.AwsStepFunctions))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class StartExecutionAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, ref TRequest? request, ref CancellationToken cancellationToken)
    {
        Console.WriteLine("[nhulston] StartExecutionAsyncIntegration.OnMethodBegin");
        return CallTargetState.GetDefault();
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        Console.WriteLine("[nhulston] StartExecutionAsyncIntegration.OnAsyncMethodEnd");
        return returnValue;
    }
}
