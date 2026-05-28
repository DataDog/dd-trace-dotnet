// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.EventBridge
{
    internal static class ContextPropagation
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContextPropagation));

        private const string DatadogKey = "_datadog";
        private const string StartTimeKey = "x-datadog-start-time";
        private const string ResourceNameKey = "x-datadog-resource-name";
        private const int MaxSizeBytes = 256 * 1024; // 256 KB

        // Loops through all entries of the EventBridge event and tries to inject Datadog context into each.
        public static void InjectContext<TPutEventsRequest>(Tracer tracer, TPutEventsRequest request, PropagationContext context)
            where TPutEventsRequest : IPutEventsRequest, IDuckType
        {
            var entries = request.Entries.Value;
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                var duckEntry = entry.DuckCast<IPutEventsRequestEntry>();
                if (duckEntry != null)
                {
                    InjectHeadersIntoDetail(tracer, duckEntry, context);
                }
            }
        }

        // Tries to add Datadog trace context under the `_datadog` key at the top level of the `detail` field.
        // `detail` is a string, so we have to manually modify it using a StringBuilder.
        private static void InjectHeadersIntoDetail(Tracer tracer, IPutEventsRequestEntry entry, PropagationContext context)
        {
            var detail = entry.Detail?.Trim() ?? "{}";
            if (!detail.EndsWith("}"))
            {
                // Unable to parse detail string, so just leave it unmodified. Don't inject trace context.
                Log.Debug("Unable to parse detail string. Not injecting trace context.");
                return;
            }

            var detailBuilder = Util.StringBuilderCache.Acquire().Append(detail);
            detailBuilder.Remove(detail.Length - 1, 1); // Remove last bracket
            if (detail.Length > 2)
            {
                detailBuilder.Append(','); // Add comma if the original detail is not empty
            }

            detailBuilder.Append($"\"{DatadogKey}\":");
            AppendContextJson(tracer, context, entry.EventBusName, detailBuilder);
            detailBuilder.Append('}');

            // Check new detail size
            var updatedDetail = Util.StringBuilderCache.GetStringAndRelease(detailBuilder);
            var byteSize = Encoding.UTF8.GetByteCount(updatedDetail);
            if (byteSize >= MaxSizeBytes)
            {
                Log.Debug("Payload size too large to pass context");
                return;
            }

            entry.Detail = updatedDetail;
        }

        // Appends the body of the Datadog trace-context JSON object (without surrounding braces) to the supplied builder.
        private static void AppendContextJson(Tracer tracer, PropagationContext context, string? eventBusName, StringBuilder jsonBuilder)
        {
            jsonBuilder.Append('{');

            tracer.TracerManager.SpanContextPropagator.Inject(context, jsonBuilder, new StringBuilderCarrierSetter());

            var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            jsonBuilder.Append($"\"{StartTimeKey}\":\"").Append(startTime).Append('"');

            if (eventBusName != null)
            {
                jsonBuilder.Append($",\"{ResourceNameKey}\":\"");
                JsonHelper.WriteEscapedJavaScriptString(jsonBuilder, eventBusName);
                jsonBuilder.Append('"');
            }

            jsonBuilder.Append('}');
        }

        private readonly struct StringBuilderCarrierSetter : ICarrierSetter<StringBuilder>
        {
            public void Set(StringBuilder carrier, string key, string value)
            {
                carrier.Append('"').Append(key).Append("\":\"");
                JsonHelper.WriteEscapedJavaScriptString(carrier, value);
                carrier.Append("\",");
            }
        }
    }
}
