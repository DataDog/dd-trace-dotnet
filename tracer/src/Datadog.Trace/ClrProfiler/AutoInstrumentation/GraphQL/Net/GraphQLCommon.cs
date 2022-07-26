// <copyright file="GraphQLCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    internal class GraphQLCommon : GraphQLCommonBase
    {
        internal const string ExecuteAsyncMethodName = "ExecuteAsync";
        internal const string ReturnTypeName = "System.Threading.Tasks.Task`1<GraphQL.ExecutionResult>";
        internal const string ExecutionContextTypeName = "GraphQL.Execution.ExecutionContext";
        internal const string GraphQLAssembly = "GraphQL";
        internal const string GraphQLReactiveAssembly = "GraphQL.SystemReactive";
        internal const string Major2 = "2";
        internal const string Major2Minor3 = "2.3";
        internal const string Major3 = "3";
        internal const string Major4 = "4";
        internal const string Major5 = "5";

        internal const string IntegrationName = nameof(Configuration.IntegrationId.GraphQL);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.GraphQL;

        internal const string ExecuteErrorType = "GraphQL.ExecutionError";
        internal const string ValidationErrorType = "GraphQL.Validation.ValidationError";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GraphQLCommon));

        internal static Scope CreateScopeFromValidate(Tracer tracer, string documentSource)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                var tags = new GraphQLTags(GraphQLCommon.IntegrationName);
                string serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);
                scope = tracer.StartActiveInternal(ValidateOperationName, serviceName: serviceName, tags: tags);

                var span = scope.Span;
                span.Type = SpanTypes.GraphQL;
                tags.Source = documentSource;

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static Scope CreateScopeFromExecuteAsync(Tracer tracer, IExecutionContext executionContext)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                string source = executionContext.Document.OriginalQuery;
                string operationName = executionContext.Operation.Name;
                var operationType = executionContext.Operation.OperationType.ToString();
                scope = CreateScopeFromExecuteAsync(tracer, IntegrationId, new GraphQLTags(GraphQLCommon.IntegrationName), ServiceName, operationName, source, operationType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static Scope CreateScopeFromExecuteAsyncV5(Tracer tracer, IExecutionContextV5 executionContext)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                string source = executionContext.Document.Source.ToString();
                string operationName = executionContext.Operation.Name.StringValue;
                string operationType = executionContext.Operation.Operation.ToString();
                scope = CreateScopeFromExecuteAsync(tracer, IntegrationId, new GraphQLTags(GraphQLCommon.IntegrationName), ServiceName, operationName, source, operationType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static void RecordExecutionErrorsIfPresent(Span span, string errorType, IExecutionErrors executionErrors)
        {
            var errorCount = executionErrors?.Count ?? 0;

            if (errorCount > 0)
            {
                RecordExecutionErrors(span, errorType, errorCount, ConstructErrorMessage(executionErrors));
            }
        }

        private static string ConstructErrorMessage(IExecutionErrors executionErrors)
        {
            if (executionErrors == null)
            {
                return string.Empty;
            }

            var builder = Util.StringBuilderCache.Acquire(Util.StringBuilderCache.MaxBuilderSize);

            try
            {
                var tab = "    ";
                builder.AppendLine("errors: [");

                for (int i = 0; i < executionErrors.Count; i++)
                {
                    var executionError = executionErrors[i];

                    builder.AppendLine($"{tab}{{");

                    var message = executionError.Message;
                    if (message != null)
                    {
                        builder.AppendLine($"{tab + tab}\"message\": \"{message.Replace("\r", "\\r").Replace("\n", "\\n")}\",");
                    }

                    var path = executionError.Path;
                    if (path != null)
                    {
                        builder.AppendLine($"{tab + tab}\"path\": \"{string.Join(".", path)}\",");
                    }

                    var code = executionError.Code;
                    if (code != null)
                    {
                        builder.AppendLine($"{tab + tab}\"code\": \"{code}\",");
                    }

                    builder.AppendLine($"{tab + tab}\"locations\": [");
                    var locations = executionError.Locations;
                    if (locations != null)
                    {
                        foreach (var location in locations)
                        {
                            if (location.TryDuckCast<ErrorLocationStruct>(out var locationProxy))
                            {
                                builder.AppendLine($"{tab + tab + tab}{{");
                                builder.AppendLine($"{tab + tab + tab + tab}\"line\": {locationProxy.Line},");
                                builder.AppendLine($"{tab + tab + tab + tab}\"column\": {locationProxy.Column}");
                                builder.AppendLine($"{tab + tab + tab}}},");
                            }
                        }
                    }

                    builder.AppendLine($"{tab + tab}]");
                    builder.AppendLine($"{tab}}},");
                }

                builder.AppendLine("]");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating GraphQL error message.");
                Util.StringBuilderCache.Release(builder);
                return "errors: []";
            }

            return Util.StringBuilderCache.GetStringAndRelease(builder);
        }
    }
}
