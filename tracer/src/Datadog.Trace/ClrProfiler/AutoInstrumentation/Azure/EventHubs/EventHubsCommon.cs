// <copyright file="EventHubsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    internal static class EventHubsCommon
    {
        private const string LogPrefix = "[EventHubs] ";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventHubsCommon));

        /// <summary>
        /// Maps EventDataBatch instances to lists of SpanContext objects from TryAdd operations
        /// </summary>
        private static readonly ConditionalWeakTable<object, ConcurrentBag<SpanContext>> BatchToSpanContexts = new();

        /// <summary>
        /// Stores a span context associated with an EventDataBatch instance
        /// </summary>
        /// <param name="batchInstance">The EventDataBatch instance</param>
        /// <param name="spanContext">The span context to store</param>
        public static void StoreSpanContext(object batchInstance, SpanContext spanContext)
        {
            if (batchInstance == null || spanContext == null)
            {
                return;
            }

            try
            {
                var spanContexts = BatchToSpanContexts.GetValue(batchInstance, _ => new ConcurrentBag<SpanContext>());
                spanContexts.Add(spanContext);

                Log.Debug(LogPrefix + "Stored span context for batch. TraceId={TraceId}, SpanId={SpanId}", (object)spanContext.TraceId128, (object)spanContext.SpanId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, LogPrefix + "Failed to store span context for EventDataBatch");
            }
        }

        /// <summary>
        /// Retrieves and removes all stored span contexts for an EventDataBatch instance
        /// </summary>
        /// <param name="batchInstance">The EventDataBatch instance</param>
        /// <returns>Collection of stored SpanContext objects, or null if none found</returns>
        public static IEnumerable<SpanContext>? RetrieveAndClearSpanContexts(object? batchInstance)
        {
            if (batchInstance == null)
            {
                return null;
            }

            try
            {
                if (!BatchToSpanContexts.TryGetValue(batchInstance, out var spanContexts) || spanContexts.IsEmpty)
                {
                    Log.Debug(LogPrefix + "No stored span contexts found for batch");
                    return null;
                }

                // Return the stored span contexts
                var contexts = spanContexts.ToList();

                Log.Debug(LogPrefix + "Retrieved {Count} span contexts for batch send operation", (object)contexts.Count);

                // Clear the stored contexts since the batch is being sent
                BatchToSpanContexts.Remove(batchInstance);

                return contexts.Count > 0 ? contexts : null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, LogPrefix + "Failed to retrieve span contexts for EventDataBatch");
                return null;
            }
        }
    }
}
