// <copyright file="CircularChannel.Reader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;

namespace Datadog.Trace.Ci.Ipc;

internal partial class CircularChannel
{
    private class Reader : IChannelReader
    {
        private readonly Timer _pollingTimer;
        private readonly CircularChannel _channel;
        private readonly ManualResetEventSlim _pollingEventFinished;
        private Action<ArraySegment<byte>>? _callback;
        private long _disposed;

        public Reader(CircularChannel channel)
        {
            _channel = channel;
            _pollingEventFinished = new ManualResetEventSlim(true);
            _callback = null;
            _disposed = 0;
            _pollingTimer = new Timer(PollForMessages, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(PollingInterval));
        }

        private void PollForMessages(object? state)
        {
            _pollingEventFinished.Reset();
            try
            {
                if (Interlocked.Read(ref _disposed) == 1 || Interlocked.Read(ref _channel._disposed) == 1)
                {
                    Log.Error("CircularChannel: Channel is disposed. Cannot read data.");
                    return;
                }

                if (_callback is null)
                {
                    // To avoid losing messages, we stop the polling if there are no subscribers
                    return;
                }

                try
                {
                    var hasHandle = _channel._mutex.WaitOne(MutexTimeout);
                    if (!hasHandle)
                    {
                        CIVisibility.Log.Error("CircularChannel: Failed to acquire mutex within the time limit.");
                        return;
                    }
                }
                catch (AbandonedMutexException ex)
                {
                    CIVisibility.Log.Error(ex, "CircularChannel: Mutex was abandoned.");
                    return;
                }
                catch (ObjectDisposedException)
                {
                    // The mutex was disposed, nothing to do
                    return;
                }

                object? messagesToHandle = null;
                try
                {
                    using var accessor = _channel._mmf.CreateViewAccessor();
                    var writePos = accessor.ReadUInt16(0);
                    var readPos = accessor.ReadUInt16(2);
                    while (readPos != writePos)
                    {
                        var absoluteReadPos = HeaderSize + readPos;
                        if (_channel.BufferSize - absoluteReadPos < 2)
                        {
                            // Not space to read the length of the message, so we need to go back to 0
                            readPos = 0;
                            absoluteReadPos = HeaderSize;
                        }

                        var length = accessor.ReadUInt16(absoluteReadPos);

                        // Simple sanity check
                        if (length + 2 > _channel.BufferBodySize)
                        {
                            // Handle error, reset pointers, or skip
                            break;
                        }

                        var data = ArrayPool<byte>.Shared.Rent(length);

                        // Read the first part of the data
                        var firstPartLength = Math.Min(length, _channel.BufferSize - absoluteReadPos - 2);
                        if (firstPartLength > 0)
                        {
                            accessor.ReadArray(absoluteReadPos + 2, data, 0, firstPartLength);
                        }

                        // Read the second part of the data, if any, from the start of the buffer
                        var secondPartLength = length - firstPartLength;
                        if (secondPartLength > 0)
                        {
                            accessor.ReadArray(HeaderSize, data, firstPartLength, secondPartLength);
                        }

                        var nextReadPos = (ushort)((readPos + length + 2) % _channel.BufferBodySize);
                        accessor.Write(2, nextReadPos); // Update read pointer

                        // We store all the messages before releasing the mutex to avoid blocking the producer for each event call
                        var dataSegment = new ArraySegment<byte>(data, 0, length);
                        if (messagesToHandle is null)
                        {
                            messagesToHandle = dataSegment;
                        }
                        else if (messagesToHandle is ArraySegment<byte> prevItem)
                        {
                            messagesToHandle = new List<ArraySegment<byte>> { prevItem, dataSegment };
                        }
                        else if (messagesToHandle is List<ArraySegment<byte>> list)
                        {
                            list.Add(dataSegment);
                        }

                        readPos = nextReadPos; // Update local readPos to continue reading if more data is available
                    }
                }
                catch (Exception ex)
                {
                    CIVisibility.Log.Error(ex, "CircularChannel: Error while polling for messages");
                }
                finally
                {
                    try
                    {
                        _channel._mutex.ReleaseMutex();
                    }
                    catch (ObjectDisposedException)
                    {
                        // The mutex was disposed, nothing to do
                    }
                }

                // Once we have released the mutex, we can safely handle the messages
                if (messagesToHandle is List<ArraySegment<byte>> messagesList)
                {
                    foreach (var data in messagesList)
                    {
                        try
                        {
                            _callback(data);
                        }
                        catch (Exception ex)
                        {
                            CIVisibility.Log.Error(ex, "CircularChannel: Error during message event handling.");
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(data.Array!);
                        }
                    }
                }
                else if (messagesToHandle is ArraySegment<byte> data)
                {
                    try
                    {
                        _callback(data);
                    }
                    catch (Exception ex)
                    {
                        CIVisibility.Log.Error(ex, "CircularChannel: Error during message event handling.");
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(data.Array!);
                    }
                }
            }
            finally
            {
                _pollingEventFinished.Set();
            }
        }

        public void SetCallback(Action<ArraySegment<byte>> callback)
        {
            _callback = callback;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _pollingTimer.Dispose();
            _pollingEventFinished.Wait();
            _callback = null;
        }
    }
}
