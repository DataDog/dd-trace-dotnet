// <copyright file="CircularChannel.Receiver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace Datadog.Trace.Ci.Ipc;

internal partial class CircularChannel
{
    private class Receiver : IChannelReceiver
    {
        private readonly Timer _pollingTimer;
        private readonly CircularChannel _channel;
        private readonly ManualResetEventSlim _pollingEventFinished;
        private bool _disposed;

        public Receiver(CircularChannel channel)
        {
            _channel = channel;
            _pollingEventFinished = new ManualResetEventSlim(true);
            _disposed = false;
            _pollingTimer = new Timer(PollForMessages, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(PollingInterval));
        }

        public event EventHandler<byte[]>? MessageReceived;

        private void PollForMessages(object? state)
        {
            _pollingEventFinished.Reset();
            try
            {
                var channel = _channel;
                if (channel._disposed)
                {
                    return;
                }

                if (MessageReceived is null)
                {
                    // To avoid loosing messages we stop the polling if there are no subscribers
                    return;
                }

                var hasHandle = channel._mutex.WaitOne(MutexTimeout);
                if (!hasHandle)
                {
                    CIVisibility.Log.Error("CircularChannel: Failed to acquire mutex within the time limit.");
                    return;
                }

                List<byte[]>? messagesToHandle = null;
                try
                {
                    using var accessor = channel._mmf.CreateViewAccessor();
                    var writePos = accessor.ReadUInt16(0);
                    var readPos = accessor.ReadUInt16(2);
                    while (readPos != writePos)
                    {
                        messagesToHandle ??= new List<byte[]>();

                        var absoluteReadPos = HeaderSize + readPos;
                        if (channel.BufferSize - absoluteReadPos < 2)
                        {
                            // Not space to read the length of the message, so we need to go back to 0
                            readPos = 0;
                            absoluteReadPos = HeaderSize;
                        }

                        var length = accessor.ReadUInt16(absoluteReadPos);

                        // Simple sanity check
                        if (length + 2 > channel.BufferBodySize)
                        {
                            // Handle error, reset pointers, or skip
                            break;
                        }

                        var data = new byte[length];

                        // Read the first part of the data
                        var firstPartLength = Math.Min(length, channel.BufferSize - absoluteReadPos - 2);
                        accessor.ReadArray(absoluteReadPos + 2, data, 0, firstPartLength);

                        // Read the second part of the data, if any, from the start of the buffer
                        var secondPartLength = length - firstPartLength;
                        if (secondPartLength > 0)
                        {
                            accessor.ReadArray(HeaderSize, data, firstPartLength, secondPartLength);
                        }

                        var nextReadPos = (ushort)((readPos + length + 2) % channel.BufferBodySize);
                        accessor.Write(2, nextReadPos); // Update read pointer

                        // We store all the messages before releasing the mutex to avoid blocking the producer for each event call
                        messagesToHandle.Add(data);

                        readPos = nextReadPos; // Update local readPos to continue reading if more data is available
                    }
                }
                catch (Exception ex)
                {
                    CIVisibility.Log.Error(ex, "CircularChannel: Error while polling for messages");
                }
                finally
                {
                    channel._mutex.ReleaseMutex();
                }

                // Once we have released the mutex, we can safely handle the messages
                if (messagesToHandle is not null)
                {
                    foreach (var data in messagesToHandle)
                    {
                        try
                        {
                            MessageReceived?.Invoke(this, data);
                        }
                        catch (Exception ex)
                        {
                            CIVisibility.Log.Error(ex, "CircularChannel: Error during message event handling.");
                        }
                    }
                }
            }
            finally
            {
                _pollingEventFinished.Set();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _pollingEventFinished.Wait();
            _pollingTimer.Dispose();
            _disposed = true;
        }
    }
}
