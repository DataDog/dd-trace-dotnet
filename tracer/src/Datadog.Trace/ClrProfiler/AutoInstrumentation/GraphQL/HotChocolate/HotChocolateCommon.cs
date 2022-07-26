// <copyright file="HotChocolateCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    internal class HotChocolateCommon : GraphQLCommonBase
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.HotChocolate);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.HotChocolate;

        private const string ServiceName = "hotchocolate";
        internal const string ErrorType = "HotChocolate.Error";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HotChocolateCommon));

        internal static Scope CreateScopeFromExecuteAsync<T>(Tracer tracer, in T request)
            where T: IQueryRequest
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
                scope = CreateScopeFromExecuteAsync(tracer, IntegrationId, new HotChocolateTags(), ServiceName, queryOperationName, source, operationType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static Scope UpdateScopeFromExecuteAsync<T>(Tracer tracer, in T context)
            where T: IOperationContext
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            var scope = tracer.InternalActiveScope;
            var span = scope?.Span;
            if (span == null || span.OperationName != ExecuteOperationName)
            {
                // not in a Hotchocolate execution span
                return null;
            }

            try
            {
                var operation = context.Operation;
                var operationType = operation.OperationType.ToString();
                if(span.Tags is HotChocolateTags tags)
                {
                    span.ResourceName = $"{operationType} {tags.GraphQLOperationName ?? "operation"}";
                    tags.GraphQLOperationType = operationType;
                }
                else
                {
                    var operationName = span.GetTag(Trace.Tags.GraphQLOperationName);
                    span.ResourceName = $"{operationType} {operationName ?? "operation"}";
                    span.SetTag(Trace.Tags.GraphQLOperationType, operationType);
                }
                

                
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating HotChocolate scope.");
            }

            return scope;
        }

        internal static void RecordExecutionErrorsIfPresent(Span span, string errorType, System.Collections.IEnumerable errors)
        {
            var executionErrors = GetList(errors);
            var errorCount = executionErrors?.Count ?? 0;

            if (errorCount > 0)
            {
                RecordExecutionErrors(span, errorType, errorCount, ConstructErrorMessage(executionErrors));
            }
        }

        private static string ConstructErrorMessage(List<IError> executionErrors)
        {
            if (executionErrors == null)
            {
                return string.Empty;
            }

            var builder = Util.StringBuilderCache.Acquire(Util.StringBuilderCache.MaxBuilderSize);

            try
            {
                const string tab = "    ";
                builder.AppendLine("errors: [");

                for (int i = 0; i < executionErrors.Count; i++)
                {
                    var executionError = executionErrors[i];

                    builder.Append(tab).AppendLine('{');

                    var message = executionError.Message;
                    if (message != null)
                    {
                        builder.AppendLine($"{tab + tab}\"message\": \"{message.Replace("\r", "\\r").Replace("\n", "\\n")}\",");
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
                Log.Error(ex, "Error creating HotChocolate error message.");
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
    }
}
