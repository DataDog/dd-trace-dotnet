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
    internal class HotChocolateCommon
    {
        internal const string ExecuteAsyncMethodName = "ExecuteAsync";
        internal const string HotChocolateAssembly = "HotChocolate.Execution";
        internal const string Major12 = "12";

        internal const string IntegrationName = nameof(Configuration.IntegrationId.HotChocolate);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.HotChocolate;

        private const string ServiceName = "hotchocolate";
        private const string ExecuteOperationName = "hotchocolate.execute";

        internal const string ErrorType = "HotChocolate.Error";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HotChocolateCommon));

        internal static Scope CreateScopeFromExecuteAsync(Tracer tracer, IQueryRequest request)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;
            try
            {
                var operationName = request.OperationName;
                var source = request.Query?.ToString();
                var operationType = "Uncompleted";
                scope = CreateScopeFromExecuteAsync(tracer, (string)operationName, source, operationType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        private static Scope CreateScopeFromExecuteAsync(Tracer tracer, string operationName, string source, string operationType)
        {
            Scope scope;

            var tags = new HotChocolateTags();
            string serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);
            scope = tracer.StartActiveInternal(ExecuteOperationName, serviceName: serviceName, tags: tags);

            var span = scope.Span;
            span.Type = SpanTypes.GraphQL;
            span.ResourceName = $"{operationType} {operationName ?? "operation"}";
            tags.Source = source;
            tags.OperationName = operationName;
            tags.OperationType = operationType;

            tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            return scope;
        }

        internal static Scope UpdateScopeFromExecuteAsync(Tracer tracer, IWorkScheduler executionContext)
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
                var operation = executionContext.Context.Operation;
                var operationType = operation.OperationType.ToString();
                var operationName = span.GetTag(Trace.Tags.HotChocolateOperationName);
                span.ResourceName = $"{operationType} {operationName ?? "operation"}";
                span.SetTag(Trace.Tags.HotChocolateOperationType, operationType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static void RecordExecutionErrorsIfPresent(Span span, string errorType, System.Collections.IEnumerable errors)
        {
            var executionErrors = GetList(errors);
            var errorCount = executionErrors?.Count ?? 0;

            if (errorCount > 0)
            {
                span.Error = true;

                span.SetTag(Trace.Tags.ErrorMsg, $"{errorCount} error(s)");
                span.SetTag(Trace.Tags.ErrorType, errorType);
                span.SetTag(Trace.Tags.ErrorStack, ConstructErrorMessage(executionErrors));
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
