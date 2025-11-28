// <copyright file="ListBucketsAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3.BucketManagement;

/// <summary>
/// AWSSDK.S3 ListBucketsAsync CallTarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.S3",
    TypeName = "Amazon.S3.AmazonS3Client",
    MethodName = "ListBucketsAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.S3.Model.ListBucketsResponse]",
    ParameterTypeNames = ["Amazon.S3.Model.ListBucketsRequest", ClrNames.CancellationToken],
    MinimumVersion = "3.3.0",
    MaximumVersion = "4.*.*",
    IntegrationName = AwsS3Common.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ListBucketsAsyncIntegration
{
    private const string Operation = "ListBuckets";

    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest request, ref CancellationToken cancellationToken)
    {
        var scope = AwsS3Common.CreateScope(Tracer.Instance, Operation, out _);
        return new CallTargetState(scope);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
