// <copyright file="CircularChannelTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using Datadog.Trace.Ci.Ipc;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Ci.Ipc;

public class CircularChannelTests
{
    private const int BufferSize = 8192;
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
        var name = nameof(CircularChannelWriteTest) + "-" + Guid.NewGuid().ToString("n");
        using var channel = new CircularChannel(name, BufferSize);
        using var writer = channel.GetWriter();

        // Message size
        var messageSize = writer.GetMessageSize(value);

        // Calculate how many messages we can write
        var messagesCount = AvailableBufferSize / messageSize;

        using var scope = new AssertionScope();
        for (var i = 0; i < messagesCount; i++)
        {
            writer.TryWrite(value).Should().BeTrue();
        }

        // If we write one more message, we should get a false result
        writer.TryWrite(value).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(GetWriteData), DisableDiscoveryEnumeration = true)]
    public void CircularChannelReadAndWriteTest(byte[] value)
    {
        var name = nameof(CircularChannelReadAndWriteTest) + "-" + Guid.NewGuid().ToString("n");
        using var channel = new CircularChannel(name, BufferSize);
        using var writer = channel.GetWriter();
        using var receiver = channel.GetReceiver();

        // Message size
        var messageSize = writer.GetMessageSize(value);

        // Calculate how many messages we can write
        var messagesCount = AvailableBufferSize / messageSize;

        // we triplicate the number of messages to test the circular buffer
        messagesCount *= 3;

        ExceptionDispatchInfo? exceptionDispatchInfo = null;
        var countdownEvent = new CountdownEvent(messagesCount);
        receiver.MessageReceived += (sender, bytes) =>
        {
            try
            {
                using var scope = new AssertionScope();
                bytes.Should().HaveCount(value.Length);
                bytes.Should().Equal(value);
            }
            catch (Exception ex)
            {
                exceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
            }

            countdownEvent.Signal();
        };

        for (var i = 0; i < messagesCount; i++)
        {
            if (!writer.TryWrite(value))
            {
                // Wait for the receiver to process the messages before trying again
                Thread.Sleep(500);
                i--;
            }
        }

        if (!countdownEvent.Wait(10_000))
        {
            exceptionDispatchInfo?.Throw();
            throw new Exception("Timeout waiting for messages");
        }
    }
}
