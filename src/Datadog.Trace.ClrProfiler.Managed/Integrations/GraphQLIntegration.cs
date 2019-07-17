using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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

        private const string Major2 = "2";
        private const string Major2Minor3 = "2.3";

        private const string ParseOperationName = "graphql.parse"; // Instrumentation not yet implemented
        private const string ValidateOperationName = "graphql.validate";
        private const string ExecuteOperationName = "graphql.execute";
        private const string ResolveOperationName = "graphql.resolve"; // Instrumentation not yet implemented

        private const string GraphQLAssemblyName = "GraphQL";
        private const string GraphQLDocumentValidatorInterfaceName = "GraphQL.Validation.IDocumentValidator";
        private const string GraphQLExecutionResultName = "GraphQL.ExecutionResult";
        private const string GraphQLExecutionStrategyInterfaceName = "GraphQL.Execution.IExecutionStrategy";
        private const string GraphQLValidationResultInterfaceName = "GraphQL.Validation.IValidationResult";

        private const string TaskOfGraphQLExecutionResult = "System.Threading.Tasks.Task`1<" + GraphQLExecutionResultName + ">";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(GraphQLIntegration));

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="documentValidator">The instance of GraphQL.Validation.IDocumentValidator.</param>
        /// <param name="originalQuery">The source of the original GraphQL query.</param>
        /// <param name="schema">The GraphQL schema.</param>
        /// <param name="document">The GraphQL document.</param>
        /// <param name="rules">The list of validation rules.</param>
        /// <param name="userContext">The user context.</param>
        /// <param name="inputs">The input variables.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = GraphQLAssemblyName,
            TargetType = GraphQLDocumentValidatorInterfaceName,
            TargetSignatureTypes = new[] { GraphQLValidationResultInterfaceName, ClrNames.String, "GraphQL.Types.ISchema", "GraphQL.Language.AST.Document", "System.Collections.Generic.IEnumerable`1<GraphQL.Validation.IValidationRule>", ClrNames.Ignore, "GraphQL.Inputs" },
            TargetMinimumVersion = Major2Minor3,
            TargetMaximumVersion = Major2)]
        public static object Validate(
            object documentValidator,
            object originalQuery,
            object schema,
            object document,
            object rules,
            object userContext,
            object inputs,
            int opCode,
            int mdToken)
        {
            if (documentValidator == null) { throw new ArgumentNullException(nameof(documentValidator)); }

            const string methodName = nameof(Validate);

            // At runtime, get a Type object for GraphQL.ExecutionResult
            var documentValidatorInstanceType = documentValidator.GetType();
            Type graphQLExecutionResultType;
            Type documentValidatorInterfaceType;

            try
            {
                var graphQLAssembly = AppDomain.CurrentDomain.GetAssemblies()
                                        .Where(a => a.GetName().Name.Equals(GraphQLAssemblyName))
                                        .Single();
                graphQLExecutionResultType = graphQLAssembly.GetType(GraphQLExecutionResultName, throwOnError: true);
                documentValidatorInterfaceType = graphQLAssembly.GetType(GraphQLDocumentValidatorInterfaceName, throwOnError: true);
            }
            catch (Exception ex)
            {
                // This shouldn't happen because the GraphQL assembly should have been loaded to construct various other types
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error finding types in the GraphQL assembly.", ex);
                throw;
            }

            Func<object, object, object, object, object, object, object, object> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, object, object, object, object, object, object>>
                        .Start(Assembly.GetCallingAssembly(), mdToken, opCode, methodName)
                        .WithConcreteType(documentValidatorInstanceType)
                        .WithParameters(originalQuery, schema, document, rules, userContext, inputs)
                        .Build();
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error resolving {documentValidatorInterfaceType.Name}.{methodName}(...)", ex);
                throw;
            }

            using (var scope = CreateScopeFromValidate(document))
            {
                try
                {
                    var validationResult = instrumentedMethod(documentValidator, originalQuery, schema, document, rules, userContext, inputs);
                    RecordExecutionErrorsIfPresent(scope.Span, "GraphQL.Validation.ValidationError", validationResult.GetProperty("Errors").GetValueOrDefault());
                    return validationResult;
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
        /// <param name="executionStrategy">The instance of GraphQL.Execution.IExecutionStrategy.</param>
        /// <param name="context">The execution context of the GraphQL operation.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = GraphQLAssemblyName,
            TargetType = GraphQLExecutionStrategyInterfaceName,
            TargetSignatureTypes = new[] { TaskOfGraphQLExecutionResult, "GraphQL.Execution.ExecutionContext" },
            TargetMinimumVersion = Major2Minor3,
            TargetMaximumVersion = Major2)]
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
                Log.ErrorException($"Error finding types in the GraphQL assembly.", ex);
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
                Log.ErrorException($"Error resolving {executionStrategyInterfaceType.Name}.{methodName}(...)", ex);
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
            object executionContext,
            Func<object, object, object> originalMethod)
        {
            using (var scope = CreateScopeFromExecuteAsync(executionContext))
            {
                try
                {
                    var task = (Task<T>)originalMethod(executionStrategy, executionContext);
                    var executionResult = await task.ConfigureAwait(false);
                    RecordExecutionErrorsIfPresent(scope.Span, "GraphQL.ExecutionError", executionContext.GetProperty("Errors").GetValueOrDefault());
                    return executionResult;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static Scope CreateScopeFromValidate(object document)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Tracer tracer = Tracer.Instance;
            string source = document.GetProperty<string>("OriginalQuery")
                                    .GetValueOrDefault();
            string serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            Scope scope = null;

            try
            {
                scope = tracer.StartActive(ValidateOperationName, serviceName: serviceName);
                var span = scope.Span;
                span.Type = SpanTypes.GraphQL;

                span.SetTag(Tags.GraphQLSource, source);

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

        private static Scope CreateScopeFromExecuteAsync(object executionContext)
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
                                                       .GetProperty<Enum>("OperationType")
                                                       .GetValueOrDefault()
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

        private static void RecordExecutionErrorsIfPresent(Span span, string errorType, object executionErrors)
        {
            var errorCount = executionErrors.GetProperty<int>("Count").GetValueOrDefault();

            if (errorCount > 0)
            {
                span.Error = true;

                span.SetTag(Trace.Tags.ErrorMsg, $"{errorCount} error(s)");
                span.SetTag(Trace.Tags.ErrorType, errorType);
                span.SetTag(Trace.Tags.ErrorStack, ConstructErrorMessage(executionErrors));
            }
        }

        private static string ConstructErrorMessage(object executionErrors)
        {
            if (executionErrors == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var tab = "    ";
            builder.AppendLine("errors: [");

            var enumerator = executionErrors.CallMethod<IEnumerator<object>>("GetEnumerator").GetValueOrDefault();

            if (enumerator != null)
            {
                try
                {
                    while (enumerator.MoveNext())
                    {
                        var executionError = enumerator.GetProperty("Current").GetValueOrDefault();

                        builder.AppendLine($"{tab}{{");

                        var message = executionError.GetProperty<string>("Message").GetValueOrDefault();
                        if (message != null)
                        {
                            builder.AppendLine($"{tab + tab}\"message\": \"{message.Replace("\r", "\\r").Replace("\n", "\\n")}\",");
                        }

                        var path = executionError.GetProperty<IEnumerable<string>>("Path").GetValueOrDefault();
                        if (path != null)
                        {
                            builder.AppendLine($"{tab + tab}\"path\": \"{string.Join(".", path)}\",");
                        }

                        var code = executionError.GetProperty<string>("Code").GetValueOrDefault();
                        if (code != null)
                        {
                            builder.AppendLine($"{tab + tab}\"code\": \"{code}\",");
                        }

                        builder.AppendLine($"{tab + tab}\"locations\": [");
                        var locations = executionError.GetProperty<IEnumerable<object>>("Locations").GetValueOrDefault();
                        if (locations != null)
                        {
                            foreach (var location in locations)
                            {
                                var line = location.GetProperty<int>("Line").GetValueOrDefault();
                                var column = location.GetProperty<int>("Column").GetValueOrDefault();

                                builder.AppendLine($"{tab + tab + tab}{{");
                                builder.AppendLine($"{tab + tab + tab + tab}\"line\": {line},");
                                builder.AppendLine($"{tab + tab + tab + tab}\"column\": {column}");
                                builder.AppendLine($"{tab + tab + tab}}},");
                            }
                        }

                        builder.AppendLine($"{tab + tab}]");
                        builder.AppendLine($"{tab}}},");
                    }

                    enumerator.Dispose();
                }
                catch (Exception ex)
                {
                    Log.ErrorException("Error creating GraphQL error message.", ex);
                    return "errors: []";
                }
            }

            builder.AppendLine("]");

            return builder.ToString();
        }
    }
}
