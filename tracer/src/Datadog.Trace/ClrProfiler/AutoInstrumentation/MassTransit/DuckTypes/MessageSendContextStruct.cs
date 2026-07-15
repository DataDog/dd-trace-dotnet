// <copyright file="MessageSendContextStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Duck-typing struct for MassTransit.MessageSendContext&lt;T&gt;.
/// Targets MessageSendContext&lt;T&gt; directly — both loopback and transport-specific send contexts
/// (e.g. BasicPublishRabbitMqSendContext) inherit from MessageSendContext&lt;T&gt;.
/// https://raw.githubusercontent.com/MassTransit/MassTransit/refs/tags/v7.3.1/src/MassTransit/Context/MessageSendContext.cs
/// </summary>
[DuckCopy]
internal struct MessageSendContextStruct
{
    /// <summary>
    /// The message ID
    /// </summary>
    public Guid? MessageId;

    /// <summary>
    /// The conversation ID
    /// </summary>
    public Guid? ConversationId;

    /// <summary>
    /// The correlation ID
    /// </summary>
    public Guid? CorrelationId;

    /// <summary>
    /// The initiator ID (for sagas)
    /// </summary>
    public Guid? InitiatorId;

    /// <summary>
    /// The request ID (for request/response)
    /// </summary>
    public Guid? RequestId;

    /// <summary>
    /// The source address
    /// </summary>
    public Uri? SourceAddress;

    /// <summary>
    /// The destination address
    /// </summary>
    public Uri? DestinationAddress;

    /// <summary>
    /// The response address
    /// </summary>
    public Uri? ResponseAddress;

    /// <summary>
    /// The fault address
    /// </summary>
    public Uri? FaultAddress;

    /// <summary>
    /// The message headers for trace context injection.
    /// Duck-copied from the underlying DictionarySendHeaders, exposing its private _headers field.
    /// </summary>
    public DictionarySendHeadersStruct? Headers;
}
