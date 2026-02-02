// <copyright file="IConsumeContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Minimal duck-typing interface for MassTransit.ConsumeContext
/// Only includes the properties needed by MassTransitIntegration and DiagnosticObserver
/// </summary>
internal interface IConsumeContext
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
    /// Gets the initiator ID
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
    /// Gets the receive context which contains InputAddress
    /// Returns object to allow duck-typing to IReceiveContext
    /// </summary>
    object? ReceiveContext { get; }
}
