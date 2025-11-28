// <copyright file="ListBucketsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3.BucketManagement;

/// <summary>
/// AWSSDK.S3 ListBuckets CallTarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.S3",
    TypeName = "Amazon.S3.AmazonS3Client",
    MethodName = "ListBuckets",
    ReturnTypeName = "Amazon.S3.Model.ListBucketsResponse",
    ParameterTypeNames = ["Amazon.S3.Model.ListBucketsRequest"],
    MinimumVersion = "3.3.0",
    MaximumVersion = "4.*.*",
    IntegrationName = AwsS3Common.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ListBucketsIntegration
{
    private const string Operation = "ListBuckets";

    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest request)
    {
        var scope = AwsS3Common.CreateScope(Tracer.Instance, Operation, out _);
        return new CallTargetState(scope);
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return new CallTargetReturn<TReturn?>(returnValue);
    }
}
