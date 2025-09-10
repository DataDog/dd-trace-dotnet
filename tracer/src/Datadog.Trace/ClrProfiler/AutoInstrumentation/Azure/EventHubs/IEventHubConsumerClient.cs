// <copyright file="IEventHubConsumerClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    /// <summary>
    /// Duck type for EventHubConsumerClient
    /// </summary>
    internal interface IEventHubConsumerClient : IDuckType
    {
        /// <summary>
        /// Gets the consumer group
        /// </summary>
        string ConsumerGroup { get; }

        /// <summary>
        /// Gets the event hub name
        /// </summary>
        string EventHubName { get; }

        /// <summary>
        /// Gets the fully qualified namespace
        /// </summary>
        string FullyQualifiedNamespace { get; }
    }
}
