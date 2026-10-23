// <copyright file="CircularChannelTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

    // Only keeps a broken channel from hanging the test run
    private static readonly TimeSpan HangTimeout = TimeSpan.FromSeconds(30);

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

        // Calculate how many messages we can write. One byte always stays free, so a full buffer can be told
        // apart from an empty one.
        var messagesCount = (AvailableBufferSize - 1) / messageSize;

        using var scope = new AssertionScope();
        for (var i = 0; i < messagesCount; i++)
        {
            writer.TryWrite(in valueSegment).Should().BeTrue();
        }

        // If we write one more message, we should get a false result
        writer.TryWrite(in valueSegment).Should().BeFalse();
    }

    [Fact]
    public void ReaderReleasesAnAbandonedMutexBeforeReceivingMoreMessages()
    {
        var name = nameof(ReaderReleasesAnAbandonedMutexBeforeReceivingMoreMessages) + "-" + Guid.NewGuid().ToString("n");
        using var channel = new CircularChannel(name, new CircularChannelSettings { PollingInterval = 10, MutexTimeout = 200 });
        using var writer = channel.GetWriter();
        var message = new ArraySegment<byte>([1]);
        writer.TryWrite(in message).Should().BeTrue();

        // A terminated owner makes the reader's next WaitOne acquire the mutex and throw.
        var mutexName = FrameworkDescription.Instance.IsWindows() ? @"Global\" + name : name;
        var owner = new Thread(() =>
        {
            using var mutex = new Mutex(initiallyOwned: false, mutexName);
            mutex.WaitOne();
        });
        owner.Start();
        owner.Join(5_000).Should().BeTrue();

        using var firstMessageReceived = new ManualResetEventSlim();
        using var messagesReceived = new CountdownEvent(2);
        using var reader = channel.GetReader();
        reader.SetCallback(_ =>
        {
            messagesReceived.Signal();
            firstMessageReceived.Set();
        });

        firstMessageReceived.Wait(5_000).Should().BeTrue();
        // Receiving the first message alone can hide a recursive mutex acquisition leak.
        writer.TryWrite(in message).Should().BeTrue("the reader must release ownership before another thread can send");
        messagesReceived.Wait(5_000).Should().BeTrue();
    }

    [Fact]
    public void OpeningAnExistingChannelKeepsUnreadMessages()
    {
        var name = nameof(OpeningAnExistingChannelKeepsUnreadMessages) + "-" + Guid.NewGuid().ToString("n");
        var settings = new CircularChannelSettings { BufferSize = BufferSize, PollingInterval = 50 };
        using var readerChannel = new CircularChannel(name, settings);

        var valueSegment = new ArraySegment<byte>([1, 2, 3, 4]);
        using (var writerChannel = new CircularChannel(name, settings))
        {
            writerChannel.GetWriter().TryWrite(in valueSegment).Should().BeTrue();
        }

        // Another process opening the same channel must not reset the pointers and drop the unread message
        using var otherChannel = new CircularChannel(name, settings);

        byte[]? receivedValue = null;
        var received = new ManualResetEventSlim(false);
        using var reader = readerChannel.GetReader();
        reader.SetCallback(bytes =>
        {
            receivedValue = bytes.ToArray();
            received.Set();
        });

        received.Wait(HangTimeout).Should().BeTrue("the message written before the channel was reopened should still be delivered");
        receivedValue.Should().Equal(valueSegment);
    }

    [Fact]
    public void BufferNeverFillsCompletelyWithTheDefaultSize()
    {
        // Older versions marked a completely full buffer with a virtual write position past the end of the buffer.
        // With the default 64 KB buffer that overflowed the ushort write pointer, so the reader saw data that wasn't
        // there and kept re-reading the same messages while holding the lock, until the process ran out of memory.
        var name = nameof(BufferNeverFillsCompletelyWithTheDefaultSize) + "-" + Guid.NewGuid().ToString("n");
        var settings = new CircularChannelSettings { PollingInterval = 50 };
        using var writerChannel = new CircularChannel(name, settings);
        using var writer = writerChannel.GetWriter();

        // Write and read a first message, so the read position is no longer at the start of the buffer
        var message = new ArraySegment<byte>(new byte[998]);
        var messageSize = writer.GetMessageSize(in message);
        using (var firstChannel = new CircularChannel(name, settings))
        {
            var firstReceived = new ManualResetEventSlim(false);
            firstChannel.GetReader().SetCallback(_ => firstReceived.Set());
            writer.TryWrite(in message).Should().BeTrue();
            firstReceived.Wait(HangTimeout).Should().BeTrue();
        }

        var messagesCount = writerChannel.BufferBodySize / messageSize;
        for (var i = 0; i < messagesCount; i++)
        {
            writer.TryWrite(in message).Should().BeTrue();
        }

        // Checked before any reader attaches, so a regression fails here instead of spinning the reader forever
        var remainingSpace = writerChannel.BufferBodySize - (messagesCount * messageSize);
        var exactFit = new ArraySegment<byte>(new byte[remainingSpace - 2]);
        writer.TryWrite(in exactFit).Should().BeFalse("a message taking all the remaining space would leave no free byte");

        var lastMessage = new ArraySegment<byte>(new byte[remainingSpace - 3]);
        writer.TryWrite(in lastMessage).Should().BeTrue();

        var expectedSizes = Enumerable.Repeat(message.Count, messagesCount).Append(lastMessage.Count).ToArray();
        var receivedSizes = new ConcurrentQueue<int>();
        using var allReceived = new CountdownEvent(expectedSizes.Length);
        using var readerChannel = new CircularChannel(name, settings);
        var reader = readerChannel.GetReader();
        reader.SetCallback(
            bytes =>
            {
                receivedSizes.Enqueue(bytes.Count);
                allReceived.Signal();
            });

        allReceived.Wait(HangTimeout).Should().BeTrue();

        // Disposing the reader stops its polling, so anything it delivered twice is in the queue now
        reader.Dispose();
        receivedSizes.Should().Equal(expectedSizes);
    }

    [Collection(nameof(HighConcurrencyTestCollection))]
    public class ConcurrencyTests
    {
        [Theory]
        [MemberData(nameof(GetWriteData), MemberType = typeof(CircularChannelTests), DisableDiscoveryEnumeration = true)]
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
}
