// <copyright file="ReceiveMessageIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// AWSSDK.SQS ReceiveMessage calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.SQS",
        TypeName = "Amazon.SQS.AmazonSQSClient",
        MethodName = "ReceiveMessage",
        ReturnTypeName = "Amazon.SQS.Model.ReceiveMessageResponse",
        ParameterTypeNames = new[] { "Amazon.SQS.Model.ReceiveMessageRequest" },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = AwsSqsCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ReceiveMessageIntegration
    {
        private const string Operation = "ReceiveMessage";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReceiveMessageRequest">Type of the request object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="request">The request for the SQS operation</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TReceiveMessageRequest>(TTarget instance, TReceiveMessageRequest request)
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
            request.MessageAttributeNames.Add(ContextPropagation.SqsKey);

            return new CallTargetState(scope, queueName);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">Response instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn<TResponse> OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
            where TResponse : IReceiveMessageResponse
        {
            if (response.Instance != null && response.Messages.Count > 0)
            {
                var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;
                if (dataStreamsManager != null && dataStreamsManager.IsEnabled)
                {
                    var queueName = (string)state.State;
                    foreach (var o in response.Messages)
                    {
                        var message = o.DuckCast<IMessage>();
                        if (message == null)
                        {
                            continue; // should not happen
                        }

                        var adapter = new ContextPropagation.MessageAttributesAdapter(message.Attributes);
                        state.Scope.Span.Context.MergePathwayContext(dataStreamsManager.ExtractPathwayContext(adapter));

                        var sentTime = 0;
                        if (message.Attributes.TryGetValue("SentTimestamp", out var sentTimeStr) && sentTimeStr != null)
                        {
                            int.TryParse(sentTimeStr, out sentTime);
                        }

                        var edgeTags = new[] { "direction:in", $"topic:{queueName}", "type:sqs" };
                        state.Scope.Span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Consume, edgeTags, payloadSizeBytes: 0, timeInQueueMs: sentTime);
                    }
                }
            }

            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TResponse>(response);
        }
    }
}
