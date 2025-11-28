// <copyright file="PublishBatchAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS
{
    /// <summary>
    /// AWSSDK.SNS PublishBatchAsync CallTarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.SimpleNotificationService",
        TypeName = "Amazon.SimpleNotificationService.AmazonSimpleNotificationServiceClient",
        MethodName = "PublishBatchAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.SimpleNotificationService.Model.PublishBatchResponse]",
        ParameterTypeNames = new[] { "Amazon.SimpleNotificationService.Model.PublishBatchRequest", ClrNames.CancellationToken },
        MinimumVersion = "3.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = AwsSnsCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class PublishBatchAsyncIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TPublishBatchRequest>(TTarget instance, TPublishBatchRequest request, CancellationToken cancellationToken)
        {
            return AwsSnsHandlerCommon.BeforePublish(request, AwsSnsHandlerCommon.SendType.Batch);
        }

        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return response;
        }
    }
}
