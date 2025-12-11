// <copyright file="GraphQLCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    internal sealed class GraphQLCommon : GraphQLCommonBase
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
            if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                var tags = new GraphQLTags(GraphQLCommon.IntegrationName);
                string serviceName = tracer.CurrentTraceSettings.GetServiceName(ServiceName);
                scope = tracer.StartActiveInternal(ValidateOperationName, serviceName: serviceName, tags: tags);

                var span = scope.Span;
                span.Type = SpanTypes.GraphQL;
                tags.Source = documentSource;

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.CurrentTraceSettings.Settings, enabledWithGlobalSetting: false);
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
            if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
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
            if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
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
                RecordExecutionErrors(span, errorType, errorCount, ConstructErrorMessage(executionErrors), ConstructErrorEvents(executionErrors));
            }
        }

        private static string ConstructErrorMessage(IExecutionErrors executionErrors)
        {
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

                    builder.AppendLine($"{tab}{{");

                    var message = executionError.Message;
                    if (message != null)
                    {
                        builder.AppendLine($"{tab + tab}\"message\": \"{message.Replace("\r", "\\r").Replace("\n", "\\n")}\",");
                    }

                    var paths = executionError.Path;
                    if (paths != null)
                    {
                        builder.AppendLine($"{tab + tab}\"path\": \"{string.Join(".", paths)}\",");
                    }

                    var code = executionError.Code;
                    if (code != null)
                    {
                        builder.AppendLine($"{tab + tab}\"code\": \"{code}\",");
                    }

                    var locations = executionError.Locations;
                    if (locations != null)
                    {
                        ConstructErrorLocationsMessage(builder, tab, locations);
                    }

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

        private static List<SpanEvent> ConstructErrorEvents(IExecutionErrors executionErrors)
        {
            List<SpanEvent> spanEvents = [];

            try
            {
                for (int i = 0; i < executionErrors.Count; i++)
                {
                    var eventAttributes = new List<KeyValuePair<string, object>>();
                    var executionError = executionErrors[i];

                    if (executionErrors[i].Instance.TryDuckCast<IExecutionErrorExtensions>(out var executionErrorExtensions))
                    {
                        var extensions = executionErrorExtensions.Extensions;
                        if (extensions is { Count: > 0 })
                        {
                            var configuredExtensions = Tracer.Instance.Settings.GraphQLErrorExtensions;

                            foreach (var extension in extensions)
                            {
                                if (configuredExtensions.Contains(extension.Key))
                                {
                                    var key = extension.Key;
                                    var value = extension.Value ?? "null";

                                    if (value is Array array)
                                    {
                                        var builder = Util.StringBuilderCache.Acquire();

                                        try
                                        {
                                            builder.Append('[');
                                            for (var k = 0; k < array.Length; k++)
                                            {
                                                var item = array.GetValue(k);
                                                if (k > 0)
                                                {
                                                    builder.Append(',');
                                                }

                                                builder.Append(item?.ToString() ?? "null");
                                            }

                                            builder.Append(']');
                                            value = Util.StringBuilderCache.GetStringAndRelease(builder);
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine(ex);
                                            Util.StringBuilderCache.Release(builder);
                                        }
                                    }
                                    else if (!Util.SpanEventConverter.IsAllowedType(value))
                                    {
                                        value = value.ToString();
                                    }

                                    eventAttributes.Add(new KeyValuePair<string, object>($"extensions.{key}", value));
                                }
                            }
                        }
                    }

                    var message = executionError.Message;
                    if (message != null)
                    {
                        eventAttributes.Add(new KeyValuePair<string, object>("message", message));
                    }

                    var paths = executionError.Path;
                    if (paths != null)
                    {
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
                        eventAttributes.Add(new KeyValuePair<string, object>("code", code));
                    }

                    var locations = executionError.Locations;
                    if (locations is not null)
                    {
                        var joinedLocations = new List<string>();
                        foreach (var location in locations)
                        {
                            if (location.TryDuckCast<ErrorLocationStruct>(out var locationProxy))
                            {
                                joinedLocations.Add($"{locationProxy.Line}:{locationProxy.Column}");
                            }
                        }

                        eventAttributes.Add(new KeyValuePair<string, object>("locations", joinedLocations.ToArray()));
                    }

                    var stacktrace = executionError.StackTrace;
                    if (stacktrace != null)
                    {
                        eventAttributes.Add(new KeyValuePair<string, object>("stacktrace", stacktrace));
                    }

                    spanEvents.Add(new SpanEvent(name: "dd.graphql.query.error", attributes: eventAttributes));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating GraphQL error SpanEvent.");
                return spanEvents;
            }

            return spanEvents;
        }
    }
}
