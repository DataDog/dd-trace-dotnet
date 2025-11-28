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
    internal sealed class HotChocolateCommon : GraphQLCommonBase
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.HotChocolate);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.HotChocolate;

        internal const string ErrorType = "HotChocolate.Error";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HotChocolateCommon));

        internal static Scope CreateScopeFromQueryRequest<T>(Tracer tracer, in T request)
            where T : IQueryRequest
        {
            if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
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
            if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
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
            if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
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
                RecordExecutionErrors(span, errorType, errorCount, ConstructErrorMessage(executionErrors), ConstructErrorEvents(executionErrors));
            }
        }

        private static string ConstructErrorMessage(List<IError> executionErrors)
        {
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
                    var executionError = executionErrors[i];

                    builder.Append(tab).AppendLine("{");

                    var message = executionError.Message;
                    if (message != null)
                    {
                        builder.AppendLine($"{tab + tab}\"message\": \"{message.Replace("\r", "\\r").Replace("\n", "\\n")}\",");
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
                Log.Error(ex, "Error creating HotChocolate error message.");
                Util.StringBuilderCache.Release(builder);
                return "errors: []";
            }

            return Util.StringBuilderCache.GetStringAndRelease(builder);
        }

        private static List<SpanEvent> ConstructErrorEvents(List<IError> executionErrors)
        {
            List<SpanEvent> spanEvents = [];

            try
            {
                for (int i = 0; i < executionErrors.Count; i++)
                {
                    var eventAttributes = new List<KeyValuePair<string, object>>();
                    var executionError = executionErrors[i];

                    var message = executionError.Message;
                    if (message != null)
                    {
                        eventAttributes.Add(new KeyValuePair<string, object>("message", message));
                    }

                    var locations = executionError.Locations;
                    if (locations != null)
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

                    var path = executionError.Path.Name;
                    if (path != null)
                    {
                        var pathName = path is NameStringProxy proxy ? proxy.Value : path.ToString();
                        eventAttributes.Add(new KeyValuePair<string, object>("path", new[] { pathName }));
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
