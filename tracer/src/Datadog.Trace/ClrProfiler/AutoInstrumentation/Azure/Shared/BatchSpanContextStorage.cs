// <copyright file="BatchSpanContextStorage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared
{
    /// <summary>
    /// Helper class to manage span contexts for Azure message batch operations.
    /// Tracks individual message span contexts per batch for span linking purposes.
    /// Used by both Azure Service Bus and Azure Event Hubs integrations.
    /// </summary>
    internal static class BatchSpanContextStorage
    {
        // Maps batch instances (ServiceBusMessageBatch or EventDataBatch) to their collection of message span contexts
        private static readonly ConditionalWeakTable<object, ConcurrentBag<SpanContext>> BatchToSpanContexts = new();

        /// <summary>
        /// Stores a span context associated with a message that was added to a batch.
        /// </summary>
        /// <param name="batchInstance">The batch instance (ServiceBusMessageBatch or EventDataBatch)</param>
        /// <param name="spanContext">The span context to store</param>
        public static void AddSpanContext(object? batchInstance, SpanContext? spanContext)
        {
            if (batchInstance == null || spanContext == null)
            {
                return;
            }

            var spanContexts = BatchToSpanContexts.GetValue(batchInstance, _ => new ConcurrentBag<SpanContext>());
            spanContexts.Add(spanContext);
        }

        /// <summary>
        /// Retrieves all stored span contexts for a batch, converts them to span links, and removes them from storage.
        /// </summary>
        /// <param name="batchInstance">The batch instance (ServiceBusMessageBatch or EventDataBatch)</param>
        /// <returns>Collection of span links, or null if none found</returns>
        public static IEnumerable<SpanLink>? ExtractSpanContexts(object? batchInstance)
        {
            if (batchInstance == null)
            {
                return null;
            }

            if (BatchToSpanContexts.TryGetValue(batchInstance, out var spanContexts))
            {
                var contexts = spanContexts.ToArray();
                BatchToSpanContexts.Remove(batchInstance);

                if (contexts.Length == 0)
                {
                    return null;
                }

                var links = new List<SpanLink>(contexts.Length);
                foreach (var spanContext in contexts)
                {
                    links.Add(new SpanLink(spanContext));
                }

                return links;
            }

            return null;
        }
    }
}
