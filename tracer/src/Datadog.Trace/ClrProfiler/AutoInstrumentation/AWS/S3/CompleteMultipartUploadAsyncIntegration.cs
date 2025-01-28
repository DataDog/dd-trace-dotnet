// <copyright file="CompleteMultipartUploadAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3;

/// <summary>
/// AWSSDK.S3 CompleteMultipartUploadAsync CallTarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.S3",
    TypeName = "Amazon.S3.AmazonS3Client",
    MethodName = "CompleteMultipartUploadAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.S3.Model.CompleteMultipartUploadResponse]",
    ParameterTypeNames = ["Amazon.S3.Model.CompleteMultipartUploadRequest", ClrNames.CancellationToken],
    MinimumVersion = "3.3.0",
    MaximumVersion = "3.*.*",
    IntegrationName = AwsS3Common.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class CompleteMultipartUploadAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest request, ref CancellationToken cancellationToken)
    {
        Console.WriteLine("[tracer] CompleteMultipartUploadAsync start");
        return CallTargetState.GetDefault();
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        Console.WriteLine("[tracer] CompleteMultipartUploadAsync end");
        return returnValue;
    }
}
