using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracing integration for AWSSDK.SQS.
    /// </summary>
    public static class AWSSDKSQSIntegration
    {
        private const string IntegrationName = "AWS";
        private const string OperationName = "aws.command";
        private const string AgentName = "dotnet-aws-sdk";

        private const string Major3 = "3";
        private const string Major3Minor3 = "3.3";

        private const string ServiceName = "aws";
        private const string AWSCoreAssemblyName = "AWSSDK.Core";
        private const string AmazonSQSAssemblyName = "AWSSDK.SQS";
        private const string RuntimePipelineTypeName = "Amazon.Runtime.Internal.RuntimePipeline";
        private const string IExecutionContextTypeName = "Amazon.Runtime.IExecutionContext";
        private const string IResponseContextTypeName = "Amazon.Runtime.IResponseContext";

        private const string IAmazonSqsTypeName = "Amazon.SQS.IAmazonSQS";
        private const string SendMessageRequestTypeName = "Amazon.SQS.Model.SendMessageRequest";
        private const string SendMessageResponseTypeName = "Amazon.SQS.Model.SendMessageResponse";
        private const string SendMessageBatchRequestTypeName = "Amazon.SQS.Model.SendMessageBatchRequest";
        private const string SendMessageBatchResponseTypeName = "Amazon.SQS.Model.SendMessageBatchResponse";

        private static readonly ILog Log = LogProvider.GetCurrentClassLogger();

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="sqs">The instance of AmazonSQS.IAmazonSQS.</param>
        /// <param name="sendMessageRequest">The message request object.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = AmazonSQSAssemblyName,
            TargetType = IAmazonSqsTypeName,
            TargetMethod = "SendMessage",
            TargetSignatureTypes = new[] { SendMessageResponseTypeName, SendMessageRequestTypeName },
            TargetMinimumVersion = Major3Minor3,
            TargetMaximumVersion = Major3)]
        public static object SendMessage(
            object sqs,
            object sendMessageRequest,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            Func<object, object, object> instrumentedMethod;
            var sqsType = sqs.GetType();

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, object>>
                    .Start(moduleVersionPtr, mdToken, opCode, nameof(SendMessage))
                    .WithConcreteType(sqsType)
                    .WithParameters(sendMessageRequest)
                    .WithNamespaceAndNameFilters(SendMessageResponseTypeName, SendMessageRequestTypeName)
                    .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: IAmazonSqsTypeName,
                    methodName: "SendMessage",
                    instanceType: sqs.GetType().AssemblyQualifiedName);
                throw;
            }

            using (var scope = CreateScopeFromSendMessage(sendMessageRequest.GetProperty<string>("QueueUrl").GetValueOrDefault()))
            {
                try
                {
                    return instrumentedMethod(sqs, sendMessageRequest);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="sqs">The instance of AmazonSQS.IAmazonSQS.</param>
        /// <param name="queueUrl">The URL for the queue.</param>
        /// <param name="messageBody">The body of the message.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = AmazonSQSAssemblyName,
            TargetType = IAmazonSqsTypeName,
            TargetMethod = "SendMessage",
            TargetSignatureTypes = new[] { SendMessageResponseTypeName, ClrNames.String, ClrNames.String },
            TargetMinimumVersion = Major3Minor3,
            TargetMaximumVersion = Major3)]
        public static object SendMessageOnlyStrings(
            object sqs,
            string queueUrl,
            string messageBody,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            Func<object, object, object, object> instrumentedMethod;
            var sqsType = sqs.GetType();

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, object, object>>
                    .Start(moduleVersionPtr, mdToken, opCode, nameof(SendMessage))
                    .WithConcreteType(sqsType)
                    .WithParameters(queueUrl, messageBody)
                    .WithNamespaceAndNameFilters(SendMessageResponseTypeName, ClrNames.String, ClrNames.String)
                    .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: IAmazonSqsTypeName,
                    methodName: "SendMessage",
                    instanceType: sqs.GetType().AssemblyQualifiedName);
                throw;
            }

            using (var scope = CreateScopeFromSendMessage(queueUrl))
            {
                try
                {
                    return instrumentedMethod(sqs, queueUrl, messageBody);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="runtimePipeline">The instance of Amazon.Runtime.IPipelineHandler.</param>
        /// <param name="executionContext">The execution context object.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = AWSCoreAssemblyName,
            TargetType = RuntimePipelineTypeName,
            TargetMethod = "InvokeSync",
            TargetSignatureTypes = new[] { IResponseContextTypeName, IExecutionContextTypeName },
            TargetMinimumVersion = Major3Minor3,
            TargetMaximumVersion = Major3)]
        public static object InvokeSync(
            object runtimePipeline,
            object executionContext,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            Func<object, object, object> instrumentedMethod;
            var runtimePipelineType = runtimePipeline.GetType();

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, object>>
                    .Start(moduleVersionPtr, mdToken, opCode, "InvokeSync")
                    .WithConcreteType(runtimePipelineType)
                    .WithParameters(executionContext)
                    .WithNamespaceAndNameFilters(IResponseContextTypeName, IExecutionContextTypeName)
                    .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: RuntimePipelineTypeName,
                    methodName: "InvokeSync",
                    instanceType: runtimePipelineType.AssemblyQualifiedName);
                throw;
            }

            using (var scope = CreateScopeFromExecutionContext(executionContext))
            {
                try
                {
                    var responseContext = instrumentedMethod(runtimePipeline, executionContext);
                    AfterMethod(executionContext.GetProperty("ResponseContext"));

                    return responseContext;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <typeparam name="T">The generic type of the response</typeparam>
        /// <param name="runtimePipeline">The instance of Amazon.Runtime.IPipelineHandler.</param>
        /// <param name="executionContext">The execution context object.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = AWSCoreAssemblyName,
            TargetType = RuntimePipelineTypeName,
            TargetMethod = "InvokeAsync",
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<T>", IExecutionContextTypeName },
            TargetMinimumVersion = Major3Minor3,
            TargetMaximumVersion = Major3)]
        public static object InvokeAsync<T>(
            object runtimePipeline,
            object executionContext,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return InvokeAsyncInternal<T>(runtimePipeline, executionContext, opCode, mdToken, moduleVersionPtr);
        }

        private static async Task<T> InvokeAsyncInternal<T>(
            object runtimePipeline,
            object executionContext,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            Func<object, object, Task<T>> instrumentedMethod;
            var runtimePipelineType = runtimePipeline.GetType();
            var genericArgument = typeof(T);

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, Task<T>>>
                    .Start(moduleVersionPtr, mdToken, opCode, "InvokeAsync")
                    .WithConcreteType(runtimePipelineType)
                    .WithMethodGenerics(genericArgument)
                    .WithParameters(executionContext)
                    .WithNamespaceAndNameFilters(ClrNames.GenericTask, IExecutionContextTypeName)
                    .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: RuntimePipelineTypeName,
                    methodName: "InvokeAsync",
                    instanceType: runtimePipelineType.AssemblyQualifiedName);
                throw;
            }

            using (var scope = CreateScopeFromExecutionContext(executionContext))
            {
                try
                {
                    var response = await instrumentedMethod(runtimePipeline, executionContext).ConfigureAwait(false);
                    AfterMethod(executionContext.GetProperty("ResponseContext"));

                    return response;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static Scope CreateScopeFromExecutionContext(object executionContext)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Tracer tracer = Tracer.Instance;
            Scope scope = null;
            string serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            try
            {
                scope = Tracer.Instance.StartActive(OperationName, serviceName: serviceName);
                var span = scope.Span;
                span.SetTag("aws.agent", AgentName);
                // span.SetTag("aws.operation", TODO: ask Tyler what should be here);
                // attributes.getattribute

                // executionContext.GetProperty("RequestContext").OriginalRequest.QueueName
                object sdkRequest = executionContext.GetProperty("RequestContext");

                /*
                if (sdkRequest.GetProperty<string>("RequestName").Contains("CreateQueueRequest"))
                {
                    sdkRequest.GetProperty("Request").GetProperty<string>("ServiceName");
                    // SetTag("aws.queue.name", sdkRequest.Request.OriginalRequest.QueueName)
                }
                */

                // span.SetTag("aws.queue.url", queueUrl);

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: false);
                span.SetMetric(Tags.Analytics, analyticsSampleRate);
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error creating or populating scope.", ex);
            }

            return scope;
        }

        private static void AfterMethod(object responseContext)
        {
            // do stuff
            // responseContext.Response.QueueUrl
            // if (responseContext.response.Context.Response.ResponseMetadata.RequestId != null)
            // {
            //   setTag("aws.requestId", responseContext.response.Context.Response.ResponseMetadata.RequestId);
            // }
        }

        private static Scope CreateScopeFromSendMessage(string queueUrl)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Tracer tracer = Tracer.Instance;
            Scope scope = null;
            string serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            try
            {
                scope = Tracer.Instance.StartActive(OperationName, serviceName: serviceName);
                var span = scope.Span;
                span.SetTag("aws.queue.url", queueUrl);

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: false);
                span.SetMetric(Tags.Analytics, analyticsSampleRate);
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error creating or populating scope.", ex);
            }

            return scope;
        }
    }
}
