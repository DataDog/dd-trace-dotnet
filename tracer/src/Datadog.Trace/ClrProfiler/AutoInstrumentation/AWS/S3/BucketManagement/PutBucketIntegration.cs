// <copyright file="PutBucketIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3.BucketManagement;

/// <summary>
/// AWSSDK.S3 PutBucket CallTarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.S3",
    TypeName = "Amazon.S3.AmazonS3Client",
    MethodName = "PutBucket",
    ReturnTypeName = "Amazon.S3.Model.PutBucketResponse",
    ParameterTypeNames = ["Amazon.S3.Model.PutBucketRequest"],
    MinimumVersion = "3.3.0",
    MaximumVersion = "4.*.*",
    IntegrationName = AwsS3Common.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PutBucketIntegration
{
    private const string Operation = "PutBucket";

    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest request)
        where TRequest : IPutBucketRequest
    {
        if (request.Instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var scope = AwsS3Common.CreateScope(Tracer.Instance, Operation, out var tags);
        AwsS3Common.SetTags(tags, request.BucketName, null); // there is no key in a PutBucketRequest

        return new CallTargetState(scope);
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return new CallTargetReturn<TReturn?>(returnValue);
    }
}
