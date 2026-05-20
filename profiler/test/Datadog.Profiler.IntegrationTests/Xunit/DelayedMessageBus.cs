// <copyright file="DelayedMessageBus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Xunit;

/// <summary>
/// Queues messages instead of forwarding them immediately.
/// On <see cref="Dispose"/>, all queued messages are flushed to the inner bus.
/// Used by <see cref="ProfilerTestCase"/> to discard intermediate failure messages on retry.
/// Based on https://github.com/xunit/samples.xunit/blob/main/v2/RetryFactExample/DelayedMessageBus.cs
/// </summary>
internal class DelayedMessageBus : IMessageBus
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
