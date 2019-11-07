using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracing integration for AWSSDK.SQS.
    /// </summary>
    public static class AwsSqsIntegration
    {
        private const string IntegrationName = "Amazon.SQS";
        private const string OperationName = "aws.http";
        private const string AgentName = "dotnet-aws-sdk";

        private const string Major3 = "3";
        private const string Major3Minor3 = "3.3";

        private const string InvokeSyncMethod = "InvokeSync";
        private const string InvokeAsyncMethod = "InvokeAsync";

        private const string ServiceName = "aws";
        private const string AWSCoreAssemblyName = "AWSSDK.Core";
        private const string RuntimePipelineTypeName = "Amazon.Runtime.Internal.RuntimePipeline";
        private const string IExecutionContextTypeName = "Amazon.Runtime.IExecutionContext";
        private const string IResponseContextTypeName = "Amazon.Runtime.IResponseContext";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(AwsSqsIntegration));

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
            TargetMethod = InvokeSyncMethod,
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
                    .Start(moduleVersionPtr, mdToken, opCode, InvokeSyncMethod)
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
                    methodName: InvokeSyncMethod,
                    instanceType: runtimePipelineType.AssemblyQualifiedName);
                throw;
            }

            using (var scope = CreateScopeFromExecutionContext(executionContext))
            {
                try
                {
                    return instrumentedMethod(runtimePipeline, executionContext);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
                finally
                {
                    // Because we need to wait for the return to decorate some values, we need to check here to ensure exceptions don't miss decorations
                    if (scope?.Span != null)
                    {
                        AfterMethod(scope.Span, executionContext);
                    }
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
            TargetMethod = InvokeAsyncMethod,
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
                    .Start(moduleVersionPtr, mdToken, opCode, InvokeAsyncMethod)
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
                    methodName: InvokeAsyncMethod,
                    instanceType: runtimePipelineType.AssemblyQualifiedName);
                throw;
            }

            using (var scope = CreateScopeFromExecutionContext(executionContext))
            {
                try
                {
                    return await instrumentedMethod(runtimePipeline, executionContext).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
                finally
                {
                    // Because we need to wait for the return to decorate some values, we need to check here to ensure exceptions don't miss decorations
                    if (scope?.Span != null)
                    {
                        AfterMethod(scope.Span, executionContext);
                    }
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

            var tracer = Tracer.Instance;
            Scope scope = null;

            try
            {
                var serviceName = $"{tracer.DefaultServiceName}-{ServiceName}";
                scope = Tracer.Instance.StartActive(OperationName, serviceName: serviceName);
                var span = scope.Span;
                span.Type = SpanTypes.Http;
                span.SetTag(Tags.SpanKind, SpanKinds.Client);

                // AWS tags
                var sdkRequest = executionContext.GetProperty("RequestContext");
                var originalRequest = sdkRequest?.GetProperty("OriginalRequest");
                var awsQueueName = originalRequest?.GetProperty<string>("QueueName").GetValueOrDefault();
                var awsQueueUrl = originalRequest?.GetProperty<string>("QueueUrl").GetValueOrDefault();

                span.SetTag(AwsTags.AgentName, AgentName);
                span.SetTag(AwsTags.SqsQueueName, awsQueueName);
                span.SetTag(AwsTags.SqsQueueUrl, awsQueueUrl);

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: false);
                span.SetMetric(Tags.Analytics, analyticsSampleRate);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        /// <summary>
        /// Some tags are not available until returning from the request for our chosen callsite.
        /// This is used as a callback to finish decorating tags.
        /// </summary>
        /// <param name="span">The span to be decorated.</param>
        /// <param name="executionContext">An instance of Amazon.Runtime.IExecutionContext</param>
        private static void AfterMethod(Span span, object executionContext)
        {
            if (executionContext != null)
            {
                var sdkRequest = executionContext.GetProperty("RequestContext");
                var request = sdkRequest?.GetProperty("Request");

                if (request != null)
                {
                    var awsOperation = request.GetProperty<string>("RequestName").GetValueOrDefault();
                    var awsService = request.GetProperty<string>("ServiceName").GetValueOrDefault();

                    awsOperation = AwsHelpers.TrimRequestFromEnd(awsOperation);
                    span.SetTag(AwsTags.OperationName, awsOperation);
                    span.SetTag(AwsTags.ServiceName, awsService);

                    span.ResourceName = $"{AwsHelpers.TrimAmazonPrefix(awsService)}.{awsOperation}";
                }

                var requestId = executionContext.GetProperty("ResponseContext").GetProperty("Response").GetProperty("ResponseMetadata").GetProperty<string>("RequestId").GetValueOrDefault();
                span.SetTag(AwsTags.RequestId, requestId);
            }
        }
    }
}
