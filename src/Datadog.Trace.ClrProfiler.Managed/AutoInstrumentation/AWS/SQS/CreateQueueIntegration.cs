// <copyright file="CreateQueueIntegration.cs" company="Datadog">
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
    /// AWSSDK.SQS CreateQueue calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.SQS",
        TypeName = "Amazon.SQS.AmazonSQSClient",
        MethodName = "CreateQueue",
        ReturnTypeName = "Amazon.SQS.Model.CreateQueueResponse",
        ParameterTypeNames = new[] { "Amazon.SQS.Model.CreateQueueRequest" },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = AwsSqsCommon.IntegrationName)]
    public class CreateQueueIntegration
    {
        private const string Operation = "CreateQueue";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TCreateQueueRequest">Type of the request object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="request">The request for the SQS operation</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TCreateQueueRequest>(TTarget instance, TCreateQueueRequest request)
            where TCreateQueueRequest : ICreateQueueRequest, IDuckType
        {
            if (request.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var scope = AwsSqsCommon.CreateScope(Tracer.Instance, Operation, out AwsSqsTags tags);
            tags.QueueName = request.QueueName;

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
            where TResponse : ICreateQueueResponse
        {
            if (state.Scope?.Span.Tags is AwsSqsTags tags)
            {
                tags.QueueUrl = response.QueueUrl;
            }

            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TResponse>(response);
        }
    }
}
