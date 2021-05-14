using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
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
        IntegrationName = AwsConstants.IntegrationName)]
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

            var scope = AwsSqsCommon.CreateScope(Tracer.Instance, $"{AwsConstants.AwsService}.{Operation}", out AwsSqsTags tags);
            tags.Operation = Operation;
            tags.Service = AwsConstants.AwsService;
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
                tags.RequestId = response.ResponseMetadata.RequestId;
                tags.QueueUrl = response.QueueUrl;

                state.Scope.Span.SetHttpStatusCode((int)response.HttpStatusCode, isServer: false);
            }

            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TResponse>(response);
        }
    }
}
