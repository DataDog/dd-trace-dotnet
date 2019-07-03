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
        private const string OperationName = "graphql.query";
        private const string ServiceName = "graphql";

        private const string GraphQLAssemblyName = "GraphQL";
        private const string GraphQLServerCoreAssemblyName = "GraphQL.Server.Core";
        private const string GraphQLExecuterInterface = "GraphQL.Server.Internal.IGraphQLExecuter";
        private const string GraphQLExecutionResult = "GraphQL.ExecutionResult";
        private const string GraphQLInputs = "GraphQL.Inputs";
        private const string TaskOfGraphQLExecutionResult = "System.Threading.Tasks.Task`1<" + GraphQLExecutionResult + ">";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(GraphQLIntegration));

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="graphQLExecuter">The IGraphQLExecuter</param>
        /// <param name="operationName">The operation name string.</param>
        /// <param name="query">The query string.</param>
        /// <param name="variables">The input variables.</param>
        /// <param name="context">The context.</param>
        /// <param name="cancellationTokenSource">A cancellation token source.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = GraphQLServerCoreAssemblyName,
            TargetType = GraphQLExecuterInterface,
            TargetSignatureTypes = new[] { TaskOfGraphQLExecutionResult, ClrNames.String, ClrNames.String, ClrNames.Ignore, ClrNames.Ignore, ClrNames.CancellationToken })]
        public static object ExecuteAsync(object graphQLExecuter, object operationName, object query, object variables, object context, object cancellationTokenSource, int opCode, int mdToken)
        {
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;

            if (graphQLExecuter == null) { throw new ArgumentNullException(nameof(graphQLExecuter)); }

            const string methodName = nameof(ExecuteAsync);

            // At runtime, get a Type object for GraphQL.ExecutionResult
            var graphQLExecuterType = graphQLExecuter.GetType();
            Type graphQLExecutionResultType;
            Type graphQLInputsType;
            Type graphQLExecuterInterfaceType;

            try
            {
                var graphQLAssembly = AppDomain.CurrentDomain.GetAssemblies()
                                        .Where(a => a.GetName().Name.Equals(GraphQLAssemblyName))
                                        .Single();
                graphQLExecutionResultType = graphQLAssembly.GetType(GraphQLExecutionResult, throwOnError: true);
                graphQLInputsType = graphQLAssembly.GetType(GraphQLInputs, throwOnError: true);

                var graphQLServerCoreAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => a.GetName().Name.Equals(GraphQLServerCoreAssemblyName))
                        .Single();
                graphQLExecuterInterfaceType = graphQLServerCoreAssembly.GetType(GraphQLExecuterInterface, throwOnError: true);
            }
            catch (Exception ex)
            {
                // This shouldn't happen because the GraphQL and GraphQL.Server.Core assembly should have been loaded to construct various other types
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error calling {graphQLExecuterType.Name}.{methodName}(IConnection connection, CancellationToken cancellationToken)", ex);
                throw;
            }

            Func<object, object, object, object, object, CancellationToken, object> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, object, object, object, CancellationToken, object>>
                        .Start(Assembly.GetCallingAssembly(), mdToken, opCode, methodName)
                        .WithConcreteType(graphQLExecuterType)
                        .WithParameters(operationName, query, variables, context, cancellationToken)
                        .Build();
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error resolving {graphQLExecuterType.Name}.{methodName}(IConnection connection, CancellationToken cancellationToken)", ex);
                throw;
            }

            // The first three arguments may be null, so we must define the parameter types explicitly
            var parameterTypes = new Type[] { typeof(string), typeof(string), graphQLInputsType, context.GetType(), typeof(CancellationToken) };

            return AsyncHelper.InvokeGenericTaskDelegateWithExplicitParameterTypes(
                owningType: graphQLExecuterInterfaceType,
                taskResultType: graphQLExecutionResultType,
                nameOfIntegrationMethod: nameof(CallGraphQLExecuteAsyncInternal),
                integrationType: typeof(GraphQLIntegration),
                parameterTypes: parameterTypes,
                graphQLExecuter,
                operationName,
                query,
                variables,
                context,
                cancellationToken,
                instrumentedMethod);
        }

        private static async Task<T> CallGraphQLExecuteAsyncInternal<T>(
            object graphQLExecuter,
            object operationName,
            object query,
            object variables,
            object context,
            CancellationToken cancellationToken,
            Func<object, object, object, object, object, CancellationToken, object> originalMethod)
        {
            using (var scope = CreateScope(graphQLExecuter, operationName, query, variables, context))
            {
                try
                {
                    var task = (Task<T>)originalMethod(graphQLExecuter, operationName, query, variables, context, cancellationToken);
                    return await task.ConfigureAwait(false);
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }

        private static Scope CreateScope(object graphQLExecuter, object operationName, object query, object variables, object context)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            // Fields
            string host = null;
            string port = null;

            // TODO: Insert stuff

            Tracer tracer = Tracer.Instance;
            string serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            Scope scope = null;

            try
            {
                scope = tracer.StartActive(OperationName, serviceName: serviceName);
                var span = scope.Span;
                span.Type = SpanTypes.GraphQL;
                // span.ResourceName = resourceName;

                // TODO: set tag graphql.source
                // TODO: set tag graphql.operation.type
                // TODO: set tag graphql.operation.name

                span.SetTag(Tags.OutHost, host);
                span.SetTag(Tags.OutPort, port);

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
