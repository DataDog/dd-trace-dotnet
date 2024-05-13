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

        foreach (var edgeCase in (int[])[0, 1, 2, 3, 4, 8190, 8191, 8192, 8193])
        {
            var bytes = new byte[edgeCase];
            random.NextBytes(bytes);
            yield return [bytes];
        }
    }

    [Theory]
    [MemberData(nameof(GetWriteData), DisableDiscoveryEnumeration = true)]
    public void CircularChannelWriteTest(byte[] value)
    {
        var name = nameof(CircularChannelWriteTest) + "-" + Guid.NewGuid().ToString("n");
        using var channel = new CircularChannel(name, new CircularChannelSettings { BufferSize = BufferSize });
        using var writer = channel.GetWriter();

        var valueSegment = new ArraySegment<byte>(value);

        // Message size
        var messageSize = writer.GetMessageSize(in valueSegment);

        // Calculate how many messages we can write
        var messagesCount = AvailableBufferSize / messageSize;

        using var scope = new AssertionScope();
        for (var i = 0; i < messagesCount; i++)
        {
            writer.TryWrite(in valueSegment).Should().BeTrue();
        }

        // If we write one more message, we should get a false result
        writer.TryWrite(in valueSegment).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(GetWriteData), DisableDiscoveryEnumeration = true)]
    public void CircularChannelReadAndWriteTest(byte[] value)
    {
        var name = nameof(CircularChannelReadAndWriteTest) + "-" + Guid.NewGuid().ToString("n");
        using var channel = new CircularChannel(name, new CircularChannelSettings { BufferSize = BufferSize, PollingInterval = 100 });
        using var writer = channel.GetWriter();
        using var reader = channel.GetReader();

        var valueSegment = new ArraySegment<byte>(value);

        // Message size
        var messageSize = writer.GetMessageSize(in valueSegment);

        // Calculate how many messages we can write
        var messagesCount = AvailableBufferSize / messageSize;

        // we duplicate the number of messages to test the circular buffer
        messagesCount *= 2;

        ExceptionDispatchInfo? exceptionDispatchInfo = null;
        var countdownEvent = new CountdownEvent(messagesCount);
        reader.SetCallback(bytes =>
        {
            try
            {
                using var scope = new AssertionScope();
                var cValue = new byte[bytes.Count];
                Array.Copy(bytes.Array!, bytes.Offset, cValue, 0, bytes.Count);
                cValue.Should().HaveCount(value.Length);
                cValue.Should().Equal(value);
                countdownEvent.Signal();
            }
            catch (Exception ex)
            {
                exceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
            }
        });

        for (var i = 0; i < messagesCount; i++)
        {
            var retries = 0;
            while (!writer.TryWrite(in valueSegment))
            {
                // Wait for the receiver to process the messages before trying again
                Thread.Sleep(500);
                if (retries++ == 20)
                {
                    throw new Exception("Error writing messages to the channel. After 20 retries, the channel is still full.");
                }
            }
        }

        if (!countdownEvent.Wait(10_000))
        {
            exceptionDispatchInfo?.Throw();
            throw new Exception("Timeout waiting for messages");
        }
    }
}
