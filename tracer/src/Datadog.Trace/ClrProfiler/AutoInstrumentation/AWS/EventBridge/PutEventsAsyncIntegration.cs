// <copyright file="PutEventsAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.EventBridge;

/// <summary>
/// AWSSDK.EventBridge PutEventsAsync CallTarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.EventBridge",
    TypeName = "Amazon.EventBridge.AmazonEventBridgeClient",
    MethodName = "PutEventsAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.EventBridge.Model.PutEventsResponse]",
    ParameterTypeNames = ["Amazon.EventBridge.Model.PutEventsRequest", ClrNames.CancellationToken],
    MinimumVersion = "3.3.0",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(IntegrationId.AwsEventBridge))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class PutEventsAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, ref TRequest? request, ref CancellationToken cancellationToken)
    {
        return CallTargetState.GetDefault();
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        return returnValue;
    }
}
