using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// AWSSDK.SQS DeleteQueue calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.SQS",
        TypeName = "Amazon.SQS.AmazonSQSClient",
        MethodName = "DeleteQueue",
        ReturnTypeName = "Amazon.SQS.Model.DeleteQueueResponse",
        ParameterTypeNames = new[] { "Amazon.SQS.Model.DeleteQueueRequest" },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = AwsConstants.IntegrationName)]
    public class DeleteQueueIntegration
    {
        private const string Operation = "DeleteQueue";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TDeleteQueueRequest">Type of the DeleteQueue request object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="request">The request for the CreateQueue operation</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TDeleteQueueRequest>(TTarget instance, TDeleteQueueRequest request)
            where TDeleteQueueRequest : IAmazonSQSRequestWithQueueUrl, IDuckType
        {
            if (request.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var scope = AwsSqsCommon.CreateScope(Tracer.Instance, $"{AwsConstants.AwsService}.{Operation}", out AwsSqsTags tags);
            tags.Operation = Operation;
            tags.Service = AwsConstants.AwsService;
            tags.QueueUrl = request.QueueUrl;

            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">DeleteQueueResponse instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static CallTargetReturn<TResponse> OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, CallTargetState state)
            where TResponse : IAmazonWebServiceResponse
        {
            if (state.Scope?.Span.Tags is AwsSqsTags tags)
            {
                tags.RequestId = response.ResponseMetadata.RequestId;
            }

            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TResponse>(response);
        }
    }
}
