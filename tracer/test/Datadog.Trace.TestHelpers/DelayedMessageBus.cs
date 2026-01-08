// <copyright file="DelayedMessageBus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers;

/// <summary>
/// Used to capture messages to potentially be forwarded later. Messages are forwarded by
/// disposing of the message bus.
/// Based on https://github.com/xunit/samples.xunit/blob/main/v2/RetryFactExample/DelayedMessageBus.cs
/// </summary>
public class DelayedMessageBus : IMessageBus
{
    private readonly IMessageBus _innerBus;
    private readonly ConcurrentQueue<IMessageSinkMessage> _messages = new();

    public DelayedMessageBus(IMessageBus innerBus)
    {
        _innerBus = innerBus;
    }

    public bool QueueMessage(IMessageSinkMessage message)
    {
        _messages.Enqueue(message);

        // No way to ask the inner bus if they want to cancel without sending them the message, so
        // we just go ahead and continue always.
        return true;
    }

    public void Dispose()
    {
        while (_messages.TryDequeue(out var message))
        {
            _innerBus.QueueMessage(message);
        }
    }
}
