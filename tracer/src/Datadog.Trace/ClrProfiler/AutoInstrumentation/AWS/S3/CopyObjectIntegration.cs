// <copyright file="CopyObjectIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3;

/// <summary>
/// AWSSDK.S3 CopyObject CallTarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.S3",
    TypeName = "Amazon.S3.AmazonS3Client",
    MethodName = "CopyObject",
    ReturnTypeName = "Amazon.S3.Model.CopyObjectResponse",
    ParameterTypeNames = ["Amazon.S3.Model.CopyObjectRequest"],
    MinimumVersion = "3.3.0",
    MaximumVersion = "3.*.*",
    IntegrationName = AwsS3Common.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class CopyObjectIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest request)
    {
        Console.WriteLine("[tracer] CopyObject start");
        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        Console.WriteLine("[tracer] CopyObject end");
        return new CallTargetReturn<TReturn?>(returnValue);
    }
}
