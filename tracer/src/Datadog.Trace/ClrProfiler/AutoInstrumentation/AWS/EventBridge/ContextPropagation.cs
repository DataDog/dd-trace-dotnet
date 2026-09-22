// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
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
        public static void InjectContext<TPutEventsRequest>(Tracer tracer, TPutEventsRequest request, Scope? scope, PropagationContext context)
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
                    InjectHeadersIntoDetail(tracer, duckEntry, scope, context);
                }
            }
        }

        // Tries to add Datadog trace context under the `_datadog` key at the top level of the `detail` field.
        // `detail` is a string, so we have to manually modify it using a StringBuilder.
        private static void InjectHeadersIntoDetail(Tracer tracer, IPutEventsRequestEntry entry, Scope? scope, PropagationContext context)
        {
            var detail = entry.Detail?.Trim() ?? "{}";
            if (!detail.EndsWith("}"))
            {
                // Unable to parse detail string, so just leave it unmodified. Don't inject trace context.
                Log.Debug("Unable to parse detail string. Not injecting trace context.");
                return;
            }

            var payloadSizeBytes = Encoding.UTF8.GetByteCount(detail);
            var pathwayContext = SetDataStreamsCheckpoint(tracer, scope, entry.DetailType, entry.EventBusName, payloadSizeBytes);

            var detailBuilder = Util.StringBuilderCache.Acquire().Append(detail);
            detailBuilder.Remove(detail.Length - 1, 1); // Remove last bracket
            if (detail.Length > 2)
            {
                detailBuilder.Append(','); // Add comma if the original detail is not empty
            }

            detailBuilder.Append($"\"{DatadogKey}\":");
            AppendContextJson(tracer, context, entry.EventBusName, pathwayContext, detailBuilder);
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

        // Appends a JSON object containing Datadog trace context to the supplied builder.
        private static void AppendContextJson(Tracer tracer, PropagationContext context, string? eventBusName, PathwayContext? pathwayContext, StringBuilder jsonBuilder)
        {
            jsonBuilder.Append('{');

            tracer.TracerManager.SpanContextPropagator.Inject(context, jsonBuilder, new StringBuilderCarrierSetter());
            if (pathwayContext is not null)
            {
                tracer.TracerManager.DataStreamsManager.InjectPathwayContextAsBase64String(
                    pathwayContext,
                    new CarrierWithDelegate<StringBuilder>(jsonBuilder, setter: static (carrier, key, value) => AppendJsonProperty(carrier, key, value)));
            }

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

        private static PathwayContext? SetDataStreamsCheckpoint(Tracer tracer, Scope? scope, string? detailType, string? eventBusName, long payloadSizeBytes)
        {
            if (scope is null)
            {
                return null;
            }

            var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
            if (!dataStreamsManager.IsEnabled)
            {
                return null;
            }

            var busName = AwsEventBridgeCommon.GetEventBusNameOrDefault(eventBusName);
            // Keep tag ordering aligned with the Go implementation for cross-runtime parity.
            var edgeTags = dataStreamsManager.GetOrCreateEdgeTags(
                new EventBridgeEdgeTagCacheKey(busName, detailType ?? string.Empty),
                static k => ["direction:out", $"exchange:{k.EventBusName}", $"topic:{k.DetailType}", "type:eventbridge"]);

            scope.Span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, payloadSizeBytes, timeInQueueMs: 0);
            return scope.Span.Context.PathwayContext;
        }

        private static void AppendJsonProperty(StringBuilder carrier, string key, string value)
        {
            carrier.Append('"').Append(key).Append("\":\"");
            JsonHelper.WriteEscapedJavaScriptString(carrier, value);
            carrier.Append("\",");
        }

        private readonly struct StringBuilderCarrierSetter : ICarrierSetter<StringBuilder>
        {
            public void Set(StringBuilder carrier, string key, string value)
            {
                AppendJsonProperty(carrier, key, value);
            }
        }
    }
}
