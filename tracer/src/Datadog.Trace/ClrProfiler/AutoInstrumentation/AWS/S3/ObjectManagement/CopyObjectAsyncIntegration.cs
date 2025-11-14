// <copyright file="CopyObjectAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3.ObjectManagement;

/// <summary>
/// AWSSDK.S3 CopyObjectAsync CallTarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.S3",
    TypeName = "Amazon.S3.AmazonS3Client",
    MethodName = "CopyObjectAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.S3.Model.CopyObjectResponse]",
    ParameterTypeNames = ["Amazon.S3.Model.CopyObjectRequest", ClrNames.CancellationToken],
    MinimumVersion = "3.3.0",
    MaximumVersion = "4.*.*",
    IntegrationName = AwsS3Common.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class CopyObjectAsyncIntegration
{
    private const string Operation = "CopyObject";

    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest request, ref CancellationToken cancellationToken)
        where TRequest : ICopyObjectRequest
    {
        if (request.Instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var tracer = Tracer.Instance;
        var scope = AwsS3Common.CreateScope(tracer, Operation, out var tags);
        AwsS3Common.SetTags(tags, request.DestinationBucketName, request.DestinationObjectKey);
        if (tags != null)
        {
            bool isOutbound = (tags.SpanKind == SpanKinds.Client) || (tags.SpanKind == SpanKinds.Producer);
            bool isServerless = EnvironmentHelpers.IsAwsLambda();
            if (isServerless && isOutbound && tags.AwsRegion != null)
            {
                if (tags.BucketName != null)
                {
                    tags.PeerService = tags.BucketName + ".s3." + tags.AwsRegion + ".amazonaws.com";
                }
                else
                {
                    tags.PeerService = "s3." + tags.AwsRegion + ".amazonaws.com";
                }

                tags.PeerServiceSource = "peer.service";
            }
            else if (!isServerless && isOutbound)
            {
                tags.PeerService = tags.BucketName;
                tags.PeerServiceSource = Trace.Tags.BucketName;
            }

            tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);
        }

        return new CallTargetState(scope, request);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
        where TReturn : IS3EtagResponse
    {
        if (Tracer.Instance.Settings.SpanPointersEnabled && state.Scope is not null && state.State is ICopyObjectRequest request && returnValue?.Instance is not null)
        {
            var bucketName = request.DestinationBucketName;
            var key = request.DestinationObjectKey;
            var eTag = returnValue.ETag;
            SpanPointers.AddS3SpanPointer(state.Scope.Span, bucketName, key, eTag);
        }

        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
