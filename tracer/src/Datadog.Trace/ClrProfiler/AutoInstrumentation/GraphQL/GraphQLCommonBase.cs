// <copyright file="GraphQLCommonBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
{
    internal abstract class GraphQLCommonBase
    {
        protected const string ParseOperationName = "graphql.parse"; // Instrumentation not yet implemented
        protected const string ValidateOperationName = "graphql.validate";
        protected const string ExecuteOperationName = "graphql.execute";
        protected const string ResolveOperationName = "graphql.resolve"; // Instrumentation not yet implemented

        protected const string ServiceName = "graphql";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GraphQLCommonBase));

        protected static Scope CreateScopeFromExecuteAsync(Tracer tracer, IntegrationId integrationId, GraphQLTags tags, string serviceName, string queryOperationName, string source, string queryOperationType)
        {
            var scope = tracer.StartActiveInternal(ExecuteOperationName, serviceName: tracer.CurrentTraceSettings.GetServiceName(tracer, serviceName), tags: tags);
            var span = scope.Span;
            span.Type = SpanTypes.GraphQL;
            span.ResourceName = $"{queryOperationType} {queryOperationName ?? "operation"}";

            tags.Source = source;
            tags.OperationName = queryOperationName;
            tags.OperationType = queryOperationType;

            tags.SetAnalyticsSampleRate(integrationId, tracer.Settings, enabledWithGlobalSetting: false);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(integrationId);
            return scope;
        }

        protected static void RecordExecutionErrors(Span span, string errorType, int errorCount, string errors, List<SpanEvent> spanEvents)
        {
            if (errorCount > 0)
            {
                span.Error = true;
                span.SetTag(Trace.Tags.ErrorMsg, $"{errorCount} error(s)");
                span.SetTag(Trace.Tags.ErrorType, errorType);
                span.SetTag(Trace.Tags.ErrorStack, errors);

                for (int i = 0; i < spanEvents.Count; i++)
                {
                    span.AddEvent(spanEvents[i]);
                }
            }
        }

        protected static void ConstructErrorLocationsMessage(StringBuilder builder, string tab, IEnumerable locations)
        {
            builder.AppendLine($"{tab + tab}\"locations\": [");
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

            builder.AppendLine($"{tab + tab}]");
        }
    }
}
