// <copyright file="ReceiveMessageAsyncIntegration.cs" company="Datadog">
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
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// AWSSDK.SQS ReceiveMessageAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.SQS",
        TypeName = "Amazon.SQS.AmazonSQSClient",
        MethodName = "ReceiveMessageAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.SQS.Model.ReceiveMessageResponse]",
        ParameterTypeNames = new[] { "Amazon.SQS.Model.ReceiveMessageRequest", ClrNames.CancellationToken },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = AwsSqsCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ReceiveMessageAsyncIntegration
    {
        private const string Operation = "ReceiveMessage";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReceiveMessageRequest">Type of the request object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="request">The request for the SQS operation</param>
        /// <param name="cancellationToken">CancellationToken value</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TReceiveMessageRequest>(TTarget instance, TReceiveMessageRequest request, CancellationToken cancellationToken)
            where TReceiveMessageRequest : IReceiveMessageRequest
        {
            if (request.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var queueName = AwsSqsCommon.GetQueueName(request.QueueUrl);
            var scope = AwsSqsCommon.CreateScope(Tracer.Instance, Operation, out var tags, spanKind: SpanKinds.Consumer);
            if (tags is not null && request.QueueUrl is not null)
            {
                tags.QueueUrl = request.QueueUrl;
                tags.QueueName = queueName;
            }

            // request the message attributes that a datadog instrumentation might have set when sending
            request.MessageAttributeNames.AddDistinct(ContextPropagation.SqsKey);
            request.AttributeNames.AddDistinct("SentTimestamp");

            return new CallTargetState(scope, queueName);
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
            where TResponse : IReceiveMessageResponse
        {
            if (response.Instance != null && response.Messages != null && response.Messages.Count > 0 && state.State != null)
            {
                var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;
                if (dataStreamsManager != null && dataStreamsManager.IsEnabled)
                {
                    var edgeTags = new[] { "direction:in", $"topic:{(string)state.State}", "type:sqs" };
                    foreach (var o in response.Messages)
                    {
                        var message = o.DuckCast<IMessage>();
                        if (message == null)
                        {
                            continue; // should not happen
                        }

                        var sentTime = 0;
                        if (message.Attributes != null && message.Attributes.TryGetValue("SentTimestamp", out var sentTimeStr) && sentTimeStr != null)
                        {
                            int.TryParse(sentTimeStr, out sentTime);
                        }

                        var adapter = AwsSqsHeadersAdapters.GetExtractionAdapter(message.MessageAttributes);
                        var parentPathway = dataStreamsManager.ExtractPathwayContext(adapter);
                        state.Scope.Span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Consume, edgeTags, payloadSizeBytes: 0, sentTime, parentPathway);
                    }
                }
            }

            state.Scope.DisposeWithException(exception);
            return response;
        }
    }
}
