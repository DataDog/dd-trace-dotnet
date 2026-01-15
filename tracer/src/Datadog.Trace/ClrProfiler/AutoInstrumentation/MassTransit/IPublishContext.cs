// <copyright file="IPublishContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// Duck-typing interface for MassTransit.PublishContext
    /// </summary>
    internal interface IPublishContext
    {
        /// <summary>
        /// Gets the message ID
        /// </summary>
        Guid? MessageId { get; }

        /// <summary>
        /// Gets the conversation ID
        /// </summary>
        Guid? ConversationId { get; }

        /// <summary>
        /// Gets the correlation ID
        /// </summary>
        Guid? CorrelationId { get; }

        /// <summary>
        /// Gets the initiator ID (for sagas)
        /// </summary>
        Guid? InitiatorId { get; }

        /// <summary>
        /// Gets the source address
        /// </summary>
        Uri? SourceAddress { get; }

        /// <summary>
        /// Gets the destination address
        /// </summary>
        Uri? DestinationAddress { get; }

        /// <summary>
        /// Gets the message headers (returns object to allow duck-typing, should be duck-cast to ISendHeaders)
        /// </summary>
        object? Headers { get; }
    }
}
