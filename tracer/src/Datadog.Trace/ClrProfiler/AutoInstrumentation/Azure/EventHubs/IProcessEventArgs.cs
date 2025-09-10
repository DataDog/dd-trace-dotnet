// <copyright file="IProcessEventArgs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    /// <summary>
    /// Duck type for Azure.Messaging.EventHubs.Processor.ProcessEventArgs
    /// </summary>
    internal interface IProcessEventArgs : IDuckType
    {
        /// <summary>
        /// Gets the event data
        /// </summary>
        IEventData Data { get; }

        /// <summary>
        /// Gets the partition context
        /// </summary>
        IPartitionContext Partition { get; }

        /// <summary>
        /// Gets a value indicating whether the event has data
        /// </summary>
        bool HasEvent { get; }
    }

    /// <summary>
    /// Duck type for partition context
    /// </summary>
    internal interface IPartitionContext : IDuckType
    {
        /// <summary>
        /// Gets the partition ID
        /// </summary>
        string PartitionId { get; }

        /// <summary>
        /// Gets the event hub name
        /// </summary>
        string EventHubName { get; }

        /// <summary>
        /// Gets the consumer group
        /// </summary>
        string ConsumerGroup { get; }
    }
}
