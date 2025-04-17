// <copyright file="GraphQLCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.Trace.Ci.EventModel;
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
        internal const string Major7 = "7";
        internal const string Major8 = "8";

        internal const string IntegrationName = nameof(Configuration.IntegrationId.GraphQL);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.GraphQL;

        internal const string ExecuteErrorType = "GraphQL.ExecutionError";
        internal const string ValidationErrorType = "GraphQL.Validation.ValidationError";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GraphQLCommon));
        private static readonly string[] OperationTypeProxyString =
        {
            nameof(OperationTypeProxy.Query),
            nameof(OperationTypeProxy.Mutation),
            nameof(OperationTypeProxy.Subscription)
        };

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
                string serviceName = tracer.CurrentTraceSettings.GetServiceName(tracer, ServiceName);
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
                var operationType = OperationTypeProxyString[(int)executionContext.Operation.OperationType];
                scope = CreateScopeFromExecuteAsync(tracer, IntegrationId, new GraphQLTags(GraphQLCommon.IntegrationName), ServiceName, operationName, source, operationType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static Scope CreateScopeFromExecuteAsyncV5AndV7(Tracer tracer, IExecutionContextV5AndV7 executionContext)
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
                string operationType = OperationTypeProxyString[(int)executionContext.Operation.Operation];
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
                RecordExecutionErrors(span, errorType, errorCount, ConstructErrorMessageAndEvents(executionErrors, out List<SpanEvent> errorSpanEvents), errorSpanEvents);
            }
        }

        private static string ConstructErrorMessageAndEvents(IExecutionErrors executionErrors, out List<SpanEvent> spanEvents)
        {
            spanEvents = [];

            if (executionErrors == null)
            {
                return string.Empty;
            }

            var builder = Util.StringBuilderCache.Acquire();

            try
            {
                var tab = "    ";
                builder.AppendLine("errors: [");

                for (int i = 0; i < executionErrors.Count; i++)
                {
                    var executionError = executionErrors[i];
                    var eventAttributes = new List<KeyValuePair<string, object>>();

                    builder.AppendLine($"{tab}{{");

                    var message = executionError.Message;
                    if (message != null)
                    {
                        builder.AppendLine($"{tab + tab}\"message\": \"{message.Replace("\r", "\\r").Replace("\n", "\\n")}\",");
                        eventAttributes.Add(new KeyValuePair<string, object>("message", message));
                    }

                    var paths = executionError.Path;
                    if (paths != null)
                    {
                        builder.AppendLine($"{tab + tab}\"path\": \"{string.Join(".", paths)}\",");

                        var pathAttribute = new List<string>();
                        foreach (var path in paths)
                        {
                            pathAttribute.Add(path.ToString());
                        }

                        eventAttributes.Add(new KeyValuePair<string, object>("path", pathAttribute.ToArray()));
                    }

                    var code = executionError.Code;
                    if (code != null)
                    {
                        builder.AppendLine($"{tab + tab}\"code\": \"{code}\",");
                        eventAttributes.Add(new KeyValuePair<string, object>("code", code.ToString()));
                    }

                    var locations = executionError.Locations;
                    if (locations is not null)
                    {
                        ConstructErrorLocationsMessage(builder, tab, locations);

                        var joinedLocations = new List<string>();
                        foreach (var location in (System.Collections.IEnumerable)locations)
                        {
                            if (location.TryDuckCast<ErrorLocationStruct>(out var locationProxy))
                            {
                                joinedLocations.Add($"{locationProxy.Line}:{locationProxy.Column}");
                            }
                        }

                        eventAttributes.Add(new KeyValuePair<string, object>("locations", joinedLocations.ToArray()));
                    }

                    builder.AppendLine($"{tab}}},");
                    spanEvents.Add(new SpanEvent(name: "dd.graphql.query.error", attributes: eventAttributes));
                }

                builder.AppendLine("]");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating GraphQL error message and SpanEvent.");
                Util.StringBuilderCache.Release(builder);
                return "errors: []";
            }

            return Util.StringBuilderCache.GetStringAndRelease(builder);
        }
    }
}
