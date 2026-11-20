// <copyright file="InvokeAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.LambdaSdk;

/// <summary>AWSSDK.Lambda InvokeAsync CallTarget instrumentation.</summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.Lambda",
    TypeName = "Amazon.Lambda.AmazonLambdaClient",
    MethodName = "InvokeAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.Lambda.Model.InvokeResponse]",
    ParameterTypeNames = ["Amazon.Lambda.Model.InvokeRequest", ClrNames.CancellationToken],
    MinimumVersion = "3.3.0",
    MaximumVersion = "4.*.*",
    IntegrationName = AwsLambdaSdkCommon.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class InvokeAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest request, CancellationToken cancellationToken)
        where TRequest : IInvokeRequest
        => new(AwsLambdaSdkCommon.CreateScope(Tracer.Instance, request));

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
