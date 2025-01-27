// <copyright file="PutObjectIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3;

/// <summary>
/// AWSSDK.S3 PutObject CallTarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.S3",
    TypeName = "Amazon.S3.AmazonS3Client",
    MethodName = "PutObject",
    ReturnTypeName = "Amazon.S3.Model.PutObjectResponse",
    ParameterTypeNames = ["Amazon.S3.Model.PutObjectRequest"],
    MinimumVersion = "3.3.0",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(IntegrationId.AwsS3))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class PutObjectIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, ref TRequest? request)
    {
        Console.WriteLine("[tracer] PutObject start.");
        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        Console.WriteLine("[tracer] PutObject start.");
        return new CallTargetReturn<TReturn?>(returnValue);
    }
}
