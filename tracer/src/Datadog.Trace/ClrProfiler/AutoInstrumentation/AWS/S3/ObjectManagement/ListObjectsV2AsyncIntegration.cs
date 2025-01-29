// <copyright file="ListObjectsV2AsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3.ObjectManagement;

/// <summary>
/// AWSSDK.S3 ListObjectsV2Async CallTarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.S3",
    TypeName = "Amazon.S3.AmazonS3Client",
    MethodName = "ListObjectsV2Async",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.S3.Model.ListObjectsV2Response]",
    ParameterTypeNames = ["Amazon.S3.Model.ListObjectsV2Request", ClrNames.CancellationToken],
    MinimumVersion = "3.3.0",
    MaximumVersion = "3.*.*",
    IntegrationName = AwsS3Common.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ListObjectsV2AsyncIntegration
{
    private const string Operation = "ListObjectsV2";

    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest request, ref CancellationToken cancellationToken)
        where TRequest : IListObjectsV2Request
    {
        if (request.Instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var scope = AwsS3Common.CreateScope(Tracer.Instance, Operation, out var tags);
        AwsS3Common.SetTags(tags, request.BucketName, null); // there is no key in a ListObjectsV2Request

        return new CallTargetState(scope);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
