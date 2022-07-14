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
        internal const string ReturnTypeName = "System.Threading.Tasks.Task";
        internal const string HotChocolateAssembly = "HotChocolate.Execution";
        internal const string Major12 = "12";

        internal const string IntegrationName = nameof(Configuration.IntegrationId.HotChocolate);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.HotChocolate;

        private const string ServiceName = "hotchocolate";
        private const string ExecuteOperationName = "hotchocolate.execute";

        internal const string ErrorType = "HotChocolate.Error";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HotChocolateCommon));

        internal static Scope CreateScopeFromExecuteAsync(Tracer tracer, IWorkScheduler executionContext)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;
            try
            {
                var operation = executionContext.Context.Operation;
                string source = operation.Document?.ToString();
                var operationType = operation.Type.ToString();
                scope = CreateScopeFromExecuteAsync(tracer, source, operationType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        private static Scope CreateScopeFromExecuteAsync(Tracer tracer, string source, string operationType)
        {
            Scope scope;

            var tags = new HotChocolateTags();
            string serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);
            scope = tracer.StartActiveInternal(ExecuteOperationName, serviceName: serviceName, tags: tags);

            var span = scope.Span;
            span.Type = SpanTypes.GraphQL;
            span.ResourceName = $"{operationType} operation";

            tags.Source = source;
            tags.OperationType = operationType;

            tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            return scope;
        }

        internal static void RecordExecutionErrorsIfPresent(Span span, string errorType, IErrors executionErrors)
        {
            var errorCount = executionErrors?.Count ?? 0;

            if (errorCount > 0)
            {
                span.Error = true;

                span.SetTag(Trace.Tags.ErrorMsg, $"{errorCount} error(s)");
                span.SetTag(Trace.Tags.ErrorType, errorType);
                span.SetTag(Trace.Tags.ErrorStack, ConstructErrorMessage(executionErrors));
            }
        }

        private static string ConstructErrorMessage(IErrors executionErrors)
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
                Log.Error(ex, "Error creating HOtChocolate error message.");
                return "errors: []";
            }

            return Util.StringBuilderCache.GetStringAndRelease(builder);
        }
    }
}
