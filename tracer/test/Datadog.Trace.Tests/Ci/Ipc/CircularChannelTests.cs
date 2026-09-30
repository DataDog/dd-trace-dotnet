// <copyright file="CircularChannelTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
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

    [SkippableFact]
    public void AbandonedMutexIsRecoveredAndTheChannelKeepsWorking()
    {
        // Outside Windows the channel uses a file lock, which can't be abandoned
        Skip.IfNot(FrameworkDescription.Instance.IsWindows(), "The named mutex lock is only used on Windows");

        var name = nameof(AbandonedMutexIsRecoveredAndTheChannelKeepsWorking) + "-" + Guid.NewGuid().ToString("n");
        using var channel = new CircularChannel(name, new CircularChannelSettings { BufferSize = BufferSize, PollingInterval = 50 });
        using var writer = channel.GetWriter();
        using var reader = channel.GetReader();

        var received = new ManualResetEventSlim(false);
        reader.SetCallback(_ => received.Set());

        // A process that dies while writing to the channel leaves the mutex abandoned.
        AbandonMutex(CircularChannel.GetMutexName(name));

        // Whichever side waits next (the polling reader or the writer below) sees the abandoned mutex.
        // An abandoned wait still transfers ownership, so that side has to release it - otherwise the
        // channel is poisoned and no process can read from or write to it again.
        var valueSegment = new ArraySegment<byte>([1, 2, 3, 4]);
        writer.TryWrite(in valueSegment).Should().BeTrue();
        received.Wait(10_000).Should().BeTrue("the message should still be delivered after the mutex was abandoned");
    }

    [SkippableFact]
    public void LockFileHeldByAnotherOwnerBlocksWritersUntilReleased()
    {
        Skip.If(FrameworkDescription.Instance.IsWindows(), "The file lock is not used on Windows");

        var path = Path.Combine(Path.GetTempPath(), "dd-trace-tests", nameof(LockFileHeldByAnotherOwnerBlocksWritersUntilReleased) + "-" + Guid.NewGuid().ToString("n"));
        using var channel = new CircularChannel(path, new CircularChannelSettings { BufferSize = BufferSize, MutexTimeout = 200 });
        using var writer = channel.GetWriter();
        var valueSegment = new ArraySegment<byte>([1, 2, 3, 4]);

        // This is what another process holding the lock looks like: a separate open file with an exclusive flock
        using (new FileStream(CircularChannel.GetLockFilePath(path), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            writer.TryWrite(in valueSegment).Should().BeFalse();
        }

        writer.TryWrite(in valueSegment).Should().BeTrue();
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
            receivedValue = new byte[bytes.Count];
            Array.Copy(bytes.Array!, bytes.Offset, receivedValue, 0, bytes.Count);
            received.Set();
        });

        received.Wait(10_000).Should().BeTrue("the message written before the channel was reopened should still be delivered");
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
            firstReceived.Wait(10_000).Should().BeTrue("the first message should be delivered");
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

        var receivedSizes = new ConcurrentQueue<int>();
        using var readerChannel = new CircularChannel(name, settings);
        using var reader = readerChannel.GetReader();
        reader.SetCallback(bytes => receivedSizes.Enqueue(bytes.Count));

        var expectedSizes = Enumerable.Repeat(message.Count, messagesCount).Append(lastMessage.Count).ToArray();
        SpinWait.SpinUntil(() => receivedSizes.Count >= expectedSizes.Length, 10_000).Should().BeTrue("every message should be delivered");

        // Give the reader a few more polls to deliver anything twice
        Thread.Sleep(settings.PollingInterval * 10);
        receivedSizes.Should().Equal(expectedSizes);
    }

    /// <summary>
    /// Acquires the named mutex on a thread that exits without releasing it, which is what the OS
    /// reports as an abandoned mutex to the next waiter.
    /// </summary>
    /// <param name="mutexName">The name of the mutex to abandon.</param>
    private static void AbandonMutex(string mutexName)
    {
        // Deliberately never disposed: the handle has to stay open until the thread has exited, and
        // it is released when the test process ends.
        Mutex? mutex = null;
        var acquired = false;
        var thread = new Thread(() =>
        {
            mutex = new Mutex(initiallyOwned: false, mutexName);
            acquired = mutex.WaitOne(5_000);
        });

        thread.Start();
        thread.Join();

        acquired.Should().BeTrue("the mutex has to be owned before it can be abandoned");
        GC.KeepAlive(mutex);
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

        [Fact]
        public async Task SeparateChannelInstancesExcludeEachOther()
        {
            // Every channel instance has its own lock handle, so this is as close as a unit test gets to several
            // processes writing to the same channel at once. Without cross-process exclusion, concurrent writers
            // overwrite each other's messages.
            const int writersCount = 4;
            const int messagesPerWriter = 500;
            const int payloadSize = 64;

            var name = nameof(SeparateChannelInstancesExcludeEachOther) + "-" + Guid.NewGuid().ToString("n");
            var settings = new CircularChannelSettings { BufferSize = BufferSize, PollingInterval = 10 };
            using var readerChannel = new CircularChannel(name, settings);
            using var reader = readerChannel.GetReader();

            // The callback only ever runs on the reader polling thread
            var lastSequences = Enumerable.Repeat(-1, writersCount).ToArray();
            string? error = null;
            var countdownEvent = new CountdownEvent(writersCount * messagesPerWriter);
            reader.SetCallback(bytes =>
            {
                var writerId = bytes.Array![bytes.Offset];
                var sequence = BitConverter.ToInt32(bytes.Array, bytes.Offset + 1);
                if (bytes.Count != payloadSize || writerId >= writersCount || sequence != lastSequences[writerId] + 1)
                {
                    error ??= $"Unexpected message: size={bytes.Count}, writer={writerId}, sequence={sequence}";
                    return;
                }

                for (var i = 5; i < payloadSize; i++)
                {
                    if (bytes.Array[bytes.Offset + i] != (byte)(writerId + sequence))
                    {
                        error ??= $"Corrupted message: writer={writerId}, sequence={sequence}";
                        return;
                    }
                }

                lastSequences[writerId] = sequence;
                countdownEvent.Signal();
            });

            var writers = new Task[writersCount];
            for (var w = 0; w < writersCount; w++)
            {
                var writerId = (byte)w;
                writers[w] = Task.Factory.StartNew(
                    () =>
                    {
                        using var channel = new CircularChannel(name, settings);
                        using var writer = channel.GetWriter();
                        var payload = new byte[payloadSize];
                        for (var sequence = 0; sequence < messagesPerWriter; sequence++)
                        {
                            payload[0] = writerId;
                            BitConverter.GetBytes(sequence).CopyTo(payload, 1);
                            for (var i = 5; i < payloadSize; i++)
                            {
                                payload[i] = (byte)(writerId + sequence);
                            }

                            var segment = new ArraySegment<byte>(payload);
                            var retries = 0;
                            while (!writer.TryWrite(in segment))
                            {
                                // The buffer is full, give the reader time to drain it
                                Thread.Sleep(10);
                                if (retries++ == 1_000)
                                {
                                    throw new Exception("Error writing messages to the channel. After 1000 retries, the channel is still full.");
                                }
                            }
                        }
                    },
                    TaskCreationOptions.LongRunning);
            }

            var allWriters = Task.WhenAll(writers);
            (await Task.WhenAny(allWriters, Task.Delay(TimeSpan.FromSeconds(60)))).Should().BeSameAs(allWriters, "the writers should finish");
            await allWriters;

            var allReceived = countdownEvent.Wait(10_000);
            error.Should().BeNull();
            allReceived.Should().BeTrue("every message should be delivered exactly once");
        }
    }
}
