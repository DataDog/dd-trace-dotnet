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
        private const string RuntimePipelineTypeName = "Amazon.Runtime.Internal.RuntimePipeline";
        private const string IExecutionContextTypeName = "Amazon.Runtime.IExecutionContext";
        private const string IResponseContextTypeName = "Amazon.Runtime.IResponseContext";

        private static readonly ILog Log = LogProvider.GetCurrentClassLogger();

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
                    AfterMethod(scope.Span, executionContext);

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
                    AfterMethod(scope.Span, executionContext);

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
                span.SetTag(Tags.SpanKind, SpanKinds.Client);

                // AWS tags
                var sdkRequest = executionContext.GetProperty("RequestContext");
                var awsQueueName = sdkRequest?.GetProperty("OriginalRequest").GetProperty<string>("QueueName").GetValueOrDefault();
                var awsQueueUrl = sdkRequest?.GetProperty("OriginalRequest").GetProperty<string>("QueueUrl").GetValueOrDefault();

                span.SetTag("aws.agent", AgentName);
                span.SetTag("aws.queue.name", awsQueueName);
                span.SetTag("aws.queue.url", awsQueueUrl);

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

        private static void AfterMethod(Span span, object executionContext)
        {
            var sdkRequest = executionContext.GetProperty("RequestContext");

            // Additional AWS tags not available until returning from the request (at least at the current callsite)
            var awsOperation = sdkRequest.GetProperty("Request").GetProperty<string>("RequestName").GetValueOrDefault();
            var awsService = sdkRequest.GetProperty("Request").GetProperty<string>("ServiceName").GetValueOrDefault();

            span.SetTag("aws.operation", awsOperation);
            span.SetTag("aws.service", awsService);

            // HTTP tags
            // Callout: Should we keep these? Java Tracer seems to add these but this span
            // is the parent of System.Net.WebRequest call that is properly traced
            // Java source: https://github.com/DataDog/dd-trace-java/blob/master/dd-java-agent/instrumentation/aws-java-sdk-2.2/src/main/java8/datadog/trace/instrumentation/aws/v2/AwsSdkClientDecorator.java
            // It uses the following HttpClientDecorator: https://github.com/DataDog/dd-trace-java/blob/master/dd-java-agent/agent-tooling/src/main/java/datadog/trace/agent/decorator/HttpClientDecorator.java
            var endpointUri = sdkRequest.GetProperty("Request").GetProperty<System.Uri>("Endpoint").GetValueOrDefault();
            var httpMethod = sdkRequest.GetProperty("Request").GetProperty<string>("HttpMethod").GetValueOrDefault();
            var httpUrl = endpointUri?.AbsoluteUri;
            var host = endpointUri?.Host;
            var port = endpointUri?.Port.ToString();

            // span.SetTag(Tags.HttpUrl, httpUrl);
            // span.SetTag(Tags.HttpMethod, httpMethod);
            // span.SetTag(Tags.OutHost, host);
            // span.SetTag(Tags.OutPort, port);

            // var sdkRequest = responseContext.GetProperty("Response").GetProperty("Context")
            // var queueName = sdkRequest?.GetProperty("OriginalRequest").GetProperty<string>("QueueName").GetValueOrDefault();
            // var queueUrl = sdkRequest?.GetProperty("OriginalRequest").GetProperty<string>("QueueUrl").GetValueOrDefault();

            // span.SetTag("aws.requestId", queueName);
            // span.SetTag("aws.queue.url", queueUrl);
            // span.SetTag("aws.service", AgentName); // TODO: Implement
            // span.SetTag("aws.operation", AgentName); // TODO: Implement

            var requestId = executionContext.GetProperty("ResponseContext").GetProperty("Response").GetProperty("ResponseMetadata").GetProperty<string>("RequestId").GetValueOrDefault();
            span.SetTag("aws.requestId", requestId);
        }
    }
}
