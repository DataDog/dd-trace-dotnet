// <copyright file="PutEventsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.EventBridge;

/// <summary>
/// AWSSDK.EventBridge PutEvents CallTarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.EventBridge",
    TypeName = "Amazon.EventBridge.AmazonEventBridgeClient",
    MethodName = "PutEvents",
    ReturnTypeName = "Amazon.EventBridge.Model.PutEventsResponse",
    ParameterTypeNames = ["Amazon.EventBridge.Model.PutEventsRequest"],
    MinimumVersion = "3.3.0",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(IntegrationId.AwsEventBridge))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class PutEventsIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, ref TRequest? request)
    {
        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        return new CallTargetReturn<TReturn?>(returnValue);
    }
}
