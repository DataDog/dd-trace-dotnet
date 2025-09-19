// <copyright file="ServiceBusBatchSpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// Helper class to manage span contexts for Service Bus message batch operations.
    /// Tracks individual message span contexts per batch for span linking purposes.
    /// </summary>
    internal static class ServiceBusBatchSpanContext
    {
        // Maps ServiceBusMessageBatch instances to their collection of message span contexts
        private static readonly ConditionalWeakTable<object, ConcurrentBag<SpanContext>> BatchToMessageSpanContexts = new();

        /// <summary>
        /// Adds a message span context to the batch's collection.
        /// </summary>
        /// <param name="batch">The ServiceBusMessageBatch instance</param>
        /// <param name="spanContext">The span context of the individual message</param>
        public static void AddMessageSpanContext(object batch, SpanContext spanContext)
        {
            if (batch == null || spanContext == null)
            {
                return;
            }

            var spanContexts = GetOrCreateMessageSpanContexts(batch);
            spanContexts.Add(spanContext);
        }

        /// <summary>
        /// Retrieves all message span contexts for a batch and optionally clears them.
        /// </summary>
        /// <param name="batch">The ServiceBusMessageBatch instance</param>
        /// <param name="clear">Whether to clear the contexts after retrieving them</param>
        /// <returns>Array of span contexts associated with the batch</returns>
        public static SpanContext[] GetMessageSpanContexts(object batch, bool clear = false)
        {
            if (batch == null)
            {
                return new SpanContext[0];
            }

            if (BatchToMessageSpanContexts.TryGetValue(batch, out var spanContexts))
            {
                var contexts = spanContexts.ToArray();

                if (clear)
                {
                    BatchToMessageSpanContexts.Remove(batch);
                }

                return contexts;
            }

            return new SpanContext[0];
        }

        /// <summary>
        /// Gets or creates the collection of message span contexts for a batch.
        /// </summary>
        /// <param name="batch">The ServiceBusMessageBatch instance</param>
        /// <returns>ConcurrentBag containing span contexts for the batch</returns>
        private static ConcurrentBag<SpanContext> GetOrCreateMessageSpanContexts(object batch)
        {
            return BatchToMessageSpanContexts.GetValue(batch, _ => new ConcurrentBag<SpanContext>());
        }
    }
}
