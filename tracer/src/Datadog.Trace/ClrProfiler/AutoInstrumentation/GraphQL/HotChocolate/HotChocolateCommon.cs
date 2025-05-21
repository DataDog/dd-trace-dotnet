// <copyright file="HotChocolateCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    internal class HotChocolateCommon : GraphQLCommonBase
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.HotChocolate);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.HotChocolate;

        internal const string ErrorType = "HotChocolate.Error";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HotChocolateCommon));

        internal static Scope CreateScopeFromQueryRequest<T>(Tracer tracer, in T request)
            where T : IQueryRequest
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;
            try
            {
                var queryOperationName = request.OperationName;
                var source = request.Query?.ToString();
                var operationType = "Uncompleted";
                scope = CreateScopeFromExecuteAsync(tracer, IntegrationId, new GraphQLTags(IntegrationName), ServiceName, queryOperationName, source, operationType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static Scope CreateScopeFromOperationRequest<T>(Tracer tracer, in T request)
            where T : IOperationRequest
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;
            try
            {
                var queryOperationName = request.OperationName;
                var source = request.Document?.ToString();
                var operationType = "Uncompleted";
                scope = CreateScopeFromExecuteAsync(tracer, IntegrationId, new GraphQLTags(HotChocolateCommon.IntegrationName), ServiceName, queryOperationName, source, operationType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static void UpdateScopeFromExecuteAsync(Tracer tracer, string operationType, string operationName)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return;
            }

            var scope = tracer.InternalActiveScope;
            var span = scope?.Span;
            if (span == null || span.OperationName != ExecuteOperationName)
            {
                // not in a Hotchocolate execution span
                return;
            }

            try
            {
                if (span.Tags is GraphQLTags tags)
                {
                    tags.OperationName = operationName;
                    span.ResourceName = $"{operationType} {tags.OperationName ?? "operation"}";
                    tags.OperationType = operationType;
                }
                else
                {
                    var operationNameTag = span.GetTag(Trace.Tags.GraphQLOperationName);
                    span.ResourceName = $"{operationType} {operationNameTag ?? "operation"}";
                    span.SetTag(Trace.Tags.GraphQLOperationType, operationType);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating HotChocolate scope.");
            }
        }

        internal static void RecordExecutionErrorsIfPresent(Span span, string errorType, System.Collections.IEnumerable errors)
        {
            var executionErrors = GetList(errors);
            var errorCount = executionErrors?.Count ?? 0;

            if (errorCount > 0)
            {
                RecordExecutionErrors(span, errorType, errorCount, ConstructErrorMessageAndEvents(executionErrors, out var errorSpanEvents), errorSpanEvents);
            }
        }

        private static string ConstructErrorMessageAndEvents(List<IError> executionErrors, out List<SpanEvent> spanEvents)
        {
            spanEvents = [];

            if (executionErrors == null)
            {
                return string.Empty;
            }

            var builder = Util.StringBuilderCache.Acquire();

            try
            {
                const string tab = "    ";
                builder.AppendLine("errors: [");

                for (int i = 0; i < executionErrors.Count; i++)
                {
                    var eventAttributes = new List<KeyValuePair<string, object>>();
                    var executionError = executionErrors[i];

                    builder.Append(tab).AppendLine("{");

                    var message = executionError.Message;
                    if (message != null)
                    {
                        builder.AppendLine($"{tab + tab}\"message\": \"{message.Replace("\r", "\\r").Replace("\n", "\\n")}\",");
                        eventAttributes.Add(new KeyValuePair<string, object>("message", message));
                    }

                    var locations = executionError.Locations;
                    if (locations != null)
                    {
                        ConstructErrorLocationsMessage(builder, tab, locations);

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

                    builder.AppendLine($"{tab}}},");

                    var path = executionError.Path;
                    if (path.Name != null)
                    {
                        var pathAttribute = new List<string>();
                        pathAttribute.Add(path.Name);
                        eventAttributes.Add(new KeyValuePair<string, object>("path", pathAttribute.ToArray()));
                    }

                    var code = executionError.Code;
                    if (code != null)
                    {
                        eventAttributes.Add(new KeyValuePair<string, object>("code", code));
                    }

                    var exception = executionError.Exception;
                    if (exception != null)
                    {
                        eventAttributes.Add(new KeyValuePair<string, object>("stacktrace", exception.StackTrace));
                    }

                    var extensions = executionError.Extensions;
                    if (extensions != null)
                    {
                        var configuredExtensions = Tracer.Instance.Settings.GraphQLErrorExtensions;

                        var keys = extensions.Keys.ToList();
                        for (int j = 0; j < keys.Count; j++)
                        {
                            if (configuredExtensions.Contains(keys[j]))
                            {
                                var key = keys[j];
                                var value = extensions[key];

                                if (value == null)
                                {
                                    value = "null";
                                }
                                else if (value is Array array)
                                {
                                    var stringArray = new string[array.Length];
                                    for (int k = 0; k < array.Length; k++)
                                    {
                                        stringArray[k] = array.GetValue(k)?.ToString() ?? "null";
                                    }

                                    value = stringArray;
                                }
                                else if (!(value is int || value is double || value is float || value is bool))
                                {
                                    value = value.ToString();
                                }

                                eventAttributes.Add(new KeyValuePair<string, object>($"extensions.{key}", value));
                            }
                        }
                    }

                    spanEvents.Add(new SpanEvent(name: "dd.graphql.query.error", attributes: eventAttributes));
                }

                builder.AppendLine("]");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating HotChocolate error message and Span Event.");
                Util.StringBuilderCache.Release(builder);
                return "errors: []";
            }

            return Util.StringBuilderCache.GetStringAndRelease(builder);
        }

        internal static List<IError> GetList(System.Collections.IEnumerable errors)
        {
            if (errors == null) { return null; }
            List<IError> res = new List<IError>();
            foreach (var error in errors)
            {
                if (error.TryDuckCast<IError>(out var err))
                {
                    res.Add(err);
                }
            }

            return res;
        }

        internal static string GetOperation(OperationTypeProxy operation)
            => operation switch
            {
                OperationTypeProxy.Query => nameof(OperationTypeProxy.Query),
                OperationTypeProxy.Mutation=> nameof(OperationTypeProxy.Mutation),
                OperationTypeProxy.Subscription=> nameof(OperationTypeProxy.Subscription),
                _ => operation.ToString(),
            };
    }
}
