using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracing integration for GraphQL.Server.Transports.AspNetCore
    /// </summary>
    public static class GraphQLIntegration
    {
        private const string IntegrationName = "GraphQL";
        private const string ServiceName = "graphql";
        private const string ExecuteOperationName = "graphql.execute";

        private const string GraphQLAssemblyName = "GraphQL";
        private const string GraphQLExecutionResultName = "GraphQL.ExecutionResult";
        private const string TaskOfGraphQLExecutionResult = "System.Threading.Tasks.Task`1<" + GraphQLExecutionResultName + ">";
        private const string GraphQLExecutionStrategyInterfaceName = "GraphQL.Execution.IExecutionStrategy";
        private static readonly ILog Log = LogProvider.GetLogger(typeof(GraphQLIntegration));

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="executionStrategy">The instance of GraphQL.Execution.IExecutionStrategy</param>
        /// <param name="context">The execution context of the GraphQL operation.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = GraphQLAssemblyName,
            TargetType = GraphQLExecutionStrategyInterfaceName,
            TargetSignatureTypes = new[] { TaskOfGraphQLExecutionResult, ClrNames.Ignore })]
        public static object ExecuteAsync(object executionStrategy, object context, int opCode, int mdToken)
        {
            if (executionStrategy == null) { throw new ArgumentNullException(nameof(executionStrategy)); }

            const string methodName = nameof(ExecuteAsync);

            // At runtime, get a Type object for GraphQL.ExecutionResult
            var executionStrategyInstanceType = executionStrategy.GetType();
            Type graphQLExecutionResultType;
            Type executionStrategyInterfaceType;

            try
            {
                var graphQLAssembly = AppDomain.CurrentDomain.GetAssemblies()
                                        .Where(a => a.GetName().Name.Equals(GraphQLAssemblyName))
                                        .Single();
                graphQLExecutionResultType = graphQLAssembly.GetType(GraphQLExecutionResultName, throwOnError: true);
                executionStrategyInterfaceType = graphQLAssembly.GetType(GraphQLExecutionStrategyInterfaceName, throwOnError: true);
            }
            catch (Exception ex)
            {
                // This shouldn't happen because the GraphQL assembly should have been loaded to construct various other types
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error calling {executionStrategyInstanceType.Name}.{methodName}(IConnection connection, CancellationToken cancellationToken)", ex);
                throw;
            }

            Func<object, object, object> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, object>>
                        .Start(Assembly.GetCallingAssembly(), mdToken, opCode, methodName)
                        .WithConcreteType(executionStrategyInstanceType)
                        .WithParameters(context)
                        .Build();
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error resolving {executionStrategyInstanceType.Name}.{methodName}(IConnection connection, CancellationToken cancellationToken)", ex);
                throw;
            }

            return AsyncHelper.InvokeGenericTaskDelegate(
                owningType: executionStrategyInterfaceType,
                taskResultType: graphQLExecutionResultType,
                nameOfIntegrationMethod: nameof(CallGraphQLExecuteAsyncInternal),
                integrationType: typeof(GraphQLIntegration),
                executionStrategy,
                context,
                instrumentedMethod);
        }

        private static async Task<T> CallGraphQLExecuteAsyncInternal<T>(
            object executionStrategy,
            object options,
            Func<object, object, object> originalMethod)
        {
            using (var scope = CreateScope(options))
            {
                try
                {
                    var task = (Task<T>)originalMethod(executionStrategy, options);
                    return await task.ConfigureAwait(false);
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }

        private static Scope CreateScope(object executionContext)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Tracer tracer = Tracer.Instance;
            string source = executionContext.GetProperty("Document")
                                            .GetProperty<string>("OriginalQuery")
                                            .GetValueOrDefault();
            string operationName = executionContext.GetProperty("Operation")
                                                   .GetProperty<string>("Name")
                                                   .GetValueOrDefault();
            string operationType = executionContext.GetProperty("Operation")
                                                   .GetProperty("OperationType")
                                                   .ToString();
            string serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            Scope scope = null;

            try
            {
                scope = tracer.StartActive(ExecuteOperationName, serviceName: serviceName);
                var span = scope.Span;
                span.Type = SpanTypes.GraphQL;
                span.ResourceName = $"{operationType} {operationName ?? "operation"}";

                span.SetTag(Tags.GraphQLSource, source);
                span.SetTag(Tags.GraphQLOperationName, operationName);
                span.SetTag(Tags.GraphQLOperationType, operationType);

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
