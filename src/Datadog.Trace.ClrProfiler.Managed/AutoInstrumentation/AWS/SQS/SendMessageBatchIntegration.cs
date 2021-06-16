// <copyright file="SendMessageBatchIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// AWSSDK.SQS SendMessageBatch calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.SQS",
        TypeName = "Amazon.SQS.AmazonSQSClient",
        MethodName = "SendMessageBatch",
        ReturnTypeName = "Amazon.SQS.Model.SendMessageBatchResponse",
        ParameterTypeNames = new[] { "Amazon.SQS.Model.SendMessageBatchRequest" },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = AwsSqsCommon.IntegrationName)]
    public class SendMessageBatchIntegration
    {
        private const string Operation = "SendMessageBatch";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TSendMessageBatchRequest">Type of the SendMessage request object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="request">The request for the SQS operation</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TSendMessageBatchRequest>(TTarget instance, TSendMessageBatchRequest request)
        {
            if (request is null)
            {
                return CallTargetState.GetDefault();
            }

            var requestProxy = request.DuckCast<ISendMessageBatchRequest>();

            var scope = AwsSqsCommon.CreateScope(Tracer.Instance, Operation, out AwsSqsTags tags);
            tags.QueueUrl = requestProxy.QueueUrl;

            if (scope?.Span?.Context != null && requestProxy.Entries.Count > 0)
            {
                for (int i = 0; i < requestProxy.Entries.Count; i++)
                {
                    var entry = requestProxy.Entries[i].DuckCast<IContainsMessageAttributes>();
                    ContextPropagation.InjectHeadersIntoMessage<TSendMessageBatchRequest>(entry, scope?.Span?.Context);
                }
            }

            return new CallTargetState(scope);
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
        public static CallTargetReturn<TResponse> OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TResponse>(response);
        }
    }
}
