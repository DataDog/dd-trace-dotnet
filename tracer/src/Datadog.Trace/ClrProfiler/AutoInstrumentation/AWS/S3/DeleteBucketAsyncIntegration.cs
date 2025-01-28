// <copyright file="DeleteBucketAsyncIntegration.cs" company="Datadog">
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
/// AWSSDK.S3 DeleteBucketAsync CallTarget instrumentation
/// DeleteBucketAsync has two overloaded methods, but the other eventually
/// call this final method, so this instrumentation captures both calls.
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.S3",
    TypeName = "Amazon.S3.AmazonS3Client",
    MethodName = "DeleteBucketAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.S3.Model.DeleteBucketResponse]",
    ParameterTypeNames = ["Amazon.S3.Model.DeleteBucketRequest", ClrNames.CancellationToken],
    MinimumVersion = "3.3.0",
    MaximumVersion = "3.*.*",
    IntegrationName = AwsS3Common.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DeleteBucketAsyncIntegration
{
    private const string Operation = "DeleteBucket";
    private const string SpanKind = SpanKinds.Producer;

    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest request, ref CancellationToken cancellationToken)
        where TRequest : IDeleteBucketRequest
    {
        if (request.Instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var scope = AwsS3Common.CreateScope(Tracer.Instance, Operation, SpanKind, out var tags);
        AwsS3Common.SetTags(tags, request.BucketName, null); // there is no key in a DeleteBucketRequest

        return new CallTargetState(scope);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
