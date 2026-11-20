// <copyright file="InvokeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.LambdaSdk;

/// <summary>AWSSDK.Lambda Invoke CallTarget instrumentation.</summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.Lambda",
    TypeName = "Amazon.Lambda.AmazonLambdaClient",
    MethodName = "Invoke",
    ReturnTypeName = "Amazon.Lambda.Model.InvokeResponse",
    ParameterTypeNames = ["Amazon.Lambda.Model.InvokeRequest"],
    MinimumVersion = "3.3.0",
    MaximumVersion = "4.*.*",
    IntegrationName = AwsLambdaSdkCommon.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class InvokeIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest request)
        where TRequest : IInvokeRequest
        => new(AwsLambdaSdkCommon.CreateScope(Tracer.Instance, request));

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return new CallTargetReturn<TReturn?>(returnValue);
    }
}
