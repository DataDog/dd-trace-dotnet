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

        public static void AddMessageSpanContext(object batch, SpanContext spanContext)
        {
            if (batch == null || spanContext == null)
            {
                return;
            }

            var spanContexts = BatchToMessageSpanContexts.GetValue(batch, _ => new ConcurrentBag<SpanContext>());
            spanContexts.Add(spanContext);
        }

        public static SpanContext[] ExtractMessageSpanContexts(object batch)
        {
            if (batch == null)
            {
                return [];
            }

            if (BatchToMessageSpanContexts.TryGetValue(batch, out var spanContexts))
            {
                var contexts = spanContexts.ToArray();
                BatchToMessageSpanContexts.Remove(batch);

                return contexts;
            }

            return [];
        }
    }
}
