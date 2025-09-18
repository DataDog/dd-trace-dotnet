// <copyright file="ServiceBusBatchSenderContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// Helper class to manage sender context for Service Bus message batch operations.
    /// Stores sender information per batch for use during TryAddMessage operations.
    /// </summary>
    internal static class ServiceBusBatchSenderContext
    {
        // Maps ServiceBusMessageBatch instances to their sender context
        private static readonly ConditionalWeakTable<object, IServiceBusSender> BatchToSenderContext = new();

        /// <summary>
        /// Stores sender context for a batch.
        /// </summary>
        /// <param name="batch">The ServiceBusMessageBatch instance</param>
        /// <param name="sender">The sender that created this batch</param>
        public static void StoreSenderContext(object batch, IServiceBusSender sender)
        {
            if (batch == null || sender == null)
            {
                return;
            }

            BatchToSenderContext.Remove(batch);
            BatchToSenderContext.Add(batch, sender);
        }

        /// <summary>
        /// Retrieves the sender context for a batch.
        /// </summary>
        /// <param name="batch">The ServiceBusMessageBatch instance</param>
        /// <returns>The sender context associated with the batch, or null if not found</returns>
        public static IServiceBusSender? GetSenderContext(object batch)
        {
            if (batch == null)
            {
                return null;
            }

            BatchToSenderContext.TryGetValue(batch, out var sender);
            return sender;
        }
    }
}
