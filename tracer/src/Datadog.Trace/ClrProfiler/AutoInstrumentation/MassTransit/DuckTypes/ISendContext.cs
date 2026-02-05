// <copyright file="ISendContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Duck-typing interface for MassTransit.SendContext
/// Used for extracting send operation metadata for tracing.
/// </summary>
internal interface ISendContext
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
    /// Gets the request ID (for request/response)
    /// </summary>
    Guid? RequestId { get; }

    /// <summary>
    /// Gets the source address
    /// </summary>
    Uri? SourceAddress { get; }

    /// <summary>
    /// Gets the destination address
    /// </summary>
    Uri? DestinationAddress { get; }

    /// <summary>
    /// Gets the response address
    /// </summary>
    Uri? ResponseAddress { get; }

    /// <summary>
    /// Gets the fault address
    /// </summary>
    Uri? FaultAddress { get; }

    /// <summary>
    /// Gets the message headers (returns object, accessed via reflection)
    /// </summary>
    object? Headers { get; }
}
