// <copyright file="SendMessageBatchAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// AWSSDK.SQS SendMessageBatchAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.SQS",
        TypeName = "Amazon.SQS.AmazonSQSClient",
        MethodName = "SendMessageBatchAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.SQS.Model.SendMessageBatchResponse]",
        ParameterTypeNames = new[] { "Amazon.SQS.Model.SendMessageBatchRequest", ClrNames.CancellationToken },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = AwsSqsCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class SendMessageBatchAsyncIntegration
    {
        private const string Operation = "SendMessageBatch";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TSendMessageBatchRequest">Type of the request object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="request">The request for the SQS operation</param>
        /// <param name="cancellationToken">CancellationToken value</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TSendMessageBatchRequest>(TTarget instance, TSendMessageBatchRequest request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return CallTargetState.GetDefault();
            }

            // we can't use generic constraints for this duck typing, because we need the original type
            // for the InjectHeadersIntoMessage<TSendMessageRequest> call below
            var requestProxy = request.DuckCast<ISendMessageBatchRequest>();

            var queueName = AwsSqsCommon.GetQueueName(requestProxy.QueueUrl);
            var scope = AwsSqsCommon.CreateScope(Tracer.Instance, Operation, out var tags, spanKind: SpanKinds.Producer);
            if (tags is not null && requestProxy.QueueUrl is not null)
            {
                tags.QueueUrl = requestProxy.QueueUrl;
                tags.QueueName = queueName;
            }

            if (scope?.Span?.Context != null && requestProxy.Entries.Count > 0 && !string.IsNullOrEmpty(queueName))
            {
                var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;
                var edgeTags = new[] { "direction:out", $"topic:{queueName}", "type:sqs" };
                foreach (var e in requestProxy.Entries)
                {
                    var entry = e.DuckCast<IContainsMessageAttributes>();
                    if (entry != null)
                    {
                        // this has no effect is DSM is disabled
                        scope.Span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0);
                        // this needs to be done for context propagation even when DSM is disabled
                        // (when DSM is enabled, it injects the pathway context on top of the trace context)
                        ContextPropagation.InjectHeadersIntoMessage<TSendMessageBatchRequest>(entry, scope.Span.Context, dataStreamsManager);
                    }
                }
            }

            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">Response instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return response;
        }
    }
}
