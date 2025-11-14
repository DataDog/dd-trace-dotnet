// <copyright file="UploadPartIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3.MultipartUploadManagement;

/// <summary>
/// AWSSDK.S3 UploadPart CallTarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.S3",
    TypeName = "Amazon.S3.AmazonS3Client",
    MethodName = "UploadPart",
    ReturnTypeName = "Amazon.S3.Model.UploadPartResponse",
    ParameterTypeNames = ["Amazon.S3.Model.UploadPartRequest"],
    MinimumVersion = "3.3.0",
    MaximumVersion = "4.*.*",
    IntegrationName = AwsS3Common.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class UploadPartIntegration
{
    private const string Operation = "UploadPart";

    internal static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest request)
        where TRequest : IUploadPartRequest
    {
        if (request.Instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var tracer = Tracer.Instance;
        var scope = AwsS3Common.CreateScope(tracer, Operation, out var tags);
        AwsS3Common.SetTags(tags, request.BucketName, request.ObjectKey);
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

        return new CallTargetState(scope);
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return new CallTargetReturn<TReturn?>(returnValue);
    }
}
