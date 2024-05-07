// <copyright file="CircularChannelTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Ci.Ipc;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci.Ipc;

public class CircularChannelTests
{
    private const int BufferSize = 65536;
    private const int HeaderSize = 4;
    private const int AvailableBufferSize = BufferSize - HeaderSize;

    public static IEnumerable<object[]> GetWriteData()
    {
        var random = new Random();
        for (var i = 311; i < AvailableBufferSize; i += 311)
        {
            var bytes = new byte[i];
            random.NextBytes(bytes);
            yield return [bytes];
        }
    }

    [Theory]
    [MemberData(nameof(GetWriteData), DisableDiscoveryEnumeration = true)]
    public void CircularChannelWriteTest(byte[] value)
    {
        using var channel = new CircularChannel(nameof(CircularChannelWriteTest));
        using var writer = channel.GetWriter();

        // Message size + 2 bytes for the message size
        var messageSize = value.Length + 2;

        // Calculate how many messages we can write
        var messagesCount = AvailableBufferSize / messageSize;

        for (var i = 0; i < messagesCount; i++)
        {
            writer.Write(value);
        }

        // If we write one more message, we should get an exception
        Assert.Throws<InvalidOperationException>(() => writer.Write(value));
    }

    [Theory]
    [MemberData(nameof(GetWriteData), DisableDiscoveryEnumeration = true)]
    public void CircularChannelReadAndWriteTest(byte[] value)
    {
        using var channel = new CircularChannel(nameof(CircularChannelReadAndWriteTest));
        using var writer = channel.GetWriter();
        using var receiver = channel.GetReceiver();

        // Message size + 2 bytes for the message size
        var messageSize = value.Length + 2;

        // Calculate how many messages we can write
        var messagesCount = AvailableBufferSize / messageSize;

        var countdownEvent = new System.Threading.CountdownEvent(messagesCount);
        receiver.MessageReceived += (sender, bytes) =>
        {
            value.SequenceEqual(bytes).Should().BeTrue();
            countdownEvent.Signal();
        };

        for (var i = 0; i < messagesCount; i++)
        {
            writer.Write(value);
        }

        if (!countdownEvent.Wait(10_000))
        {
            throw new Exception("Timeout waiting for messages");
        }
    }
}
