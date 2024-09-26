// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.EventBridge
{
    internal static class ContextPropagation
    {
        private const string DatadogKey = "_datadog";
        private const string StartTimeKey = "x-datadog-start-time";
        private const string ResourceNameKey = "x-datadog-resource-name";

        public static void InjectTracingContext<TPutEventsRequest>(TPutEventsRequest request, SpanContext context)
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
                    InjectHeadersIntoDetail(duckEntry, context, duckEntry.EventBusName);
                }
            }
        }

        private static void InjectHeadersIntoDetail(IPutEventsRequestEntry entry, SpanContext context, string? eventBusName)
        {
            var detail = entry.Detail?.Trim() ?? "{}";
            if (!detail.EndsWith("}"))
            {
                // Unable to parse detail string, so just leave it unmodified. Don't inject trace context.
                return;
            }

            var detailBuilder = Util.StringBuilderCache.Acquire(Util.StringBuilderCache.MaxBuilderSize).Append(detail);
            detailBuilder.Remove(detail.Length - 1, 1); // Remove last bracket
            if (detail.Length > 2)
            {
                detailBuilder.Append(','); // Add comma if the original detail is not empty
            }

            var traceContext = GetTraceContext(context, eventBusName);
            detailBuilder.Append($"\"{DatadogKey}\":{traceContext}").Append('}');
            entry.Detail = Util.StringBuilderCache.GetStringAndRelease(detailBuilder);
        }

        private static string GetTraceContext(SpanContext context, string? eventBusName)
        {
            // Inject trace context
            var jsonBuilder = Util.StringBuilderCache.Acquire(Util.StringBuilderCache.MaxBuilderSize);
            jsonBuilder.Append('{');
            SpanContextPropagator.Instance.Inject(context, jsonBuilder, new StringBuilderCarrierSetter());

            // Inject start time and bus name
            var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            jsonBuilder.Append($"\"{StartTimeKey}\":\"{startTime}\"");
            if (eventBusName != null)
            {
                jsonBuilder.Append($",\"{ResourceNameKey}\":\"{eventBusName}\"");
            }

            jsonBuilder.Append('}');
            return Util.StringBuilderCache.GetStringAndRelease(jsonBuilder);
        }

        private struct StringBuilderCarrierSetter : ICarrierSetter<StringBuilder>
        {
            public void Set(StringBuilder carrier, string key, string value)
            {
                carrier.AppendFormat("\"{0}\":\"{1}\",", key, value);
            }
        }
    }
}
