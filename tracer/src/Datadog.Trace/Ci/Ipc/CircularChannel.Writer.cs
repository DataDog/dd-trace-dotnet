// <copyright file="CircularChannel.Writer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading;

namespace Datadog.Trace.Ci.Ipc;

internal partial class CircularChannel
{
    private class Writer : IChannelWriter
    {
        private readonly CircularChannel _channel;
        private long _disposed;

        internal Writer(CircularChannel channel)
        {
            _disposed = 0;
            _channel = channel;
        }

        public int GetMessageSize(in ArraySegment<byte> data) => data.Count + 2;

        public bool TryWrite(in ArraySegment<byte> data)
        {
            var channel = _channel;
            if (Interlocked.Read(ref _disposed) == 1 || Interlocked.Read(ref channel._disposed) == 1)
            {
                Log.Error("CircularChannel: Channel is disposed. Cannot write data.");
                return false;
            }

            var dataSize = GetMessageSize(in data);
            if (dataSize > channel.BufferBodySize)
            {
                Log.Error("CircularChannel: Message size exceeds maximum allowed size.");
                return false;
            }

            var hasHandle = channel._mutex.WaitOne(MutexTimeout);
            if (!hasHandle)
            {
                Log.Error("CircularChannel: Failed to acquire mutex within the time limit.");
                return false;
            }

            try
            {
                using var accessor = channel._mmf.CreateViewAccessor();
                var writePos = accessor.ReadUInt16(0);
                var readPos = accessor.ReadUInt16(2);

                var absoluteWritePos = HeaderSize + writePos;
                if (channel.BufferSize - absoluteWritePos < 2)
                {
                    // Not space to write the length of the message, so we need to go back to 0
                    writePos = 0;
                    absoluteWritePos = HeaderSize;
                }

                var nextWritePos = (ushort)((writePos + dataSize) % channel.BufferBodySize);

                /*
                 Check for buffer overflow conditions to prevent data corruption:
                    For writePos < readPos (1 indicates written but not read data)
                        |1111111111|0000000000000000|11111111|
                                writePos         readPos
                    For writePos > readPos (1 indicates written but not read data)
                        |0000000000|1111111111111111|00000000|
                                readPos          writePos
                 */

                var spaceAvailable = writePos < readPos
                                         ? readPos - writePos
                                         : channel.BufferBodySize - (writePos - readPos);
                if (spaceAvailable < dataSize)
                {
                    Log.Warning("CircularChannel: Buffer overflow");
                    return false;
                }

                // Write the first part of the data
                var remainningSpace = channel.BufferBodySize - writePos;
                var firstPartLength = Math.Min(dataSize, remainningSpace) - 2;
                accessor.Write(absoluteWritePos, (ushort)data.Count);
                accessor.WriteArray(absoluteWritePos + 2, data.Array!, data.Offset, firstPartLength);

                // Write the second part of the data, if any, from the start of the buffer
                var secondPartLength = data.Count - firstPartLength;
                if (secondPartLength > 0)
                {
                    accessor.WriteArray(HeaderSize, data.Array!, firstPartLength, secondPartLength);
                }

                accessor.Write(0, nextWritePos); // Update write pointer
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CircularChannel: Error while writing data");
                return false;
            }
            finally
            {
                try
                {
                    channel._mutex.ReleaseMutex();
                }
                catch (ObjectDisposedException)
                {
                    // The mutex was disposed, nothing to do
                }
            }
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _disposed, 1);
        }
    }
}
