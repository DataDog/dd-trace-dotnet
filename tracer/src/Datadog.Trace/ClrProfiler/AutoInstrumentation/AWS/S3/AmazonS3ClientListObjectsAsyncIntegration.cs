// <copyright file="AmazonS3ClientListObjectsAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3;

/// <summary>
/// System.Threading.Tasks.Task`1[Amazon.S3.Model.ListObjectsResponse] Amazon.S3.AmazonS3Client::ListObjectsAsync(Amazon.S3.Model.ListObjectsRequest,System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.S3",
    TypeName = "AmazonS3Client",
    MethodName = "ListObjectsAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.S3.Model.ListObjectsResponse]",
    ParameterTypeNames = new[] { "Amazon.S3.Model.ListObjectsRequest", ClrNames.CancellationToken },
    MinimumVersion = "3.3.0",
    MaximumVersion = "3.*.*",
    IntegrationName = IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class AmazonS3ClientListObjectsAsyncIntegration
{
    private const string IntegrationName = nameof(IntegrationId.AwsSdk);

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TArg1">Type of the argument 1 (Amazon.S3.Model.ListObjectsRequest)</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
    /// <param name="request">Instance of Amazon.S3.Model.ListObjectsRequest</param>
    /// <param name="cancellationToken">Instance of System.Threading.CancellationToken</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, ref TArg1 request, ref CancellationToken cancellationToken)
    {
        var tracer = Tracer.Instance;
        var scope = tracer.StartActiveInternal("S3.ListObjectAsync", parent: null, tags: null, serviceName: "workshop");
        var span = scope.Span;

        span.Type = SpanTypes.Http;
        span.ResourceName = $"testingBucket";
        tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AwsSdk);
        return new CallTargetState(scope);
    }

    /// <summary>
    /// OnAsyncMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value (Amazon.S3.Model.ListObjectsResponse)</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="returnValue">Instance of Amazon.S3.Model.ListObjectsResponse</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
