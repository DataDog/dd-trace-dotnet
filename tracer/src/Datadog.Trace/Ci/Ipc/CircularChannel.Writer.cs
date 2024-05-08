// <copyright file="CircularChannel.Writer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.Ci.Ipc;

internal partial class CircularChannel
{
    private class Writer : IChannelWriter
    {
        private readonly CircularChannel _channel;
        private bool _disposed;

        internal Writer(CircularChannel channel)
        {
            _disposed = false;
            _channel = channel;
        }

        public int GetMessageSize(byte[] data) => data.Length + 2;

        public bool TryWrite(byte[] data)
        {
            var channel = _channel;
            if (channel._disposed)
            {
                Log.Error("CircularChannel: Channel is disposed. Cannot write data.");
                return false;
            }

            var dataSize = GetMessageSize(data);
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
                var writePos = channel._accessor.ReadUInt16(0);
                var readPos = channel._accessor.ReadUInt16(2);

                var absoluteWritePos = HeaderSize + writePos;
                if (channel.BufferSize - absoluteWritePos < 2)
                {
                    // Not space to write the length of the message, so we need to go back to 0
                    writePos = 0;
                    absoluteWritePos = HeaderSize;
                }

                var nextWritePos = (ushort)((writePos + dataSize) % channel.BufferBodySize);

                // Check for buffer overflow conditions to prevent data corruption:
                // 1. When writePos is less than readPos:
                //    - nextWritePos must not be greater than or equal to readPos, which would mean it has
                //      overwritten unread data that readPos has not yet reached.
                //    - nextWritePos must also not wrap around and end up less than writePos. This would
                //      indicate that it has circled back and started overwriting the beginning of the buffer,
                //      potentially corrupting data that may not have been read yet.
                // 2. When writePos is greater than readPos:
                //    - nextWritePos wrapping around and ending up between readPos and writePos would also
                //      indicate an overflow, as it would overwrite unread data between these positions.
                //
                // These conditions collectively ensure that new data does not overwrite unread data in the circular buffer,
                // maintaining the integrity of the data in scenarios where the buffer wraps or is close to wrapping.
                if ((writePos < readPos && (nextWritePos >= readPos || nextWritePos < writePos)) ||
                    (writePos > readPos && nextWritePos < writePos && nextWritePos > readPos))
                {
                    Log.Warning("CircularChannel: Buffer overflow");
                    return false;
                }

                // Write the first part of the data
                var remainningSpace = channel.BufferBodySize - writePos;
                var firstPartLength = Math.Min(dataSize, remainningSpace) - 2;
                channel._accessor.Write(absoluteWritePos, (ushort)data.Length);
                channel._accessor.WriteArray(absoluteWritePos + 2, data, 0, firstPartLength);

                // Write the second part of the data, if any, from the start of the buffer
                var secondPartLength = data.Length - firstPartLength;
                if (secondPartLength > 0)
                {
                    channel._accessor.WriteArray(HeaderSize, data, firstPartLength, secondPartLength);
                }

                channel._accessor.Write(0, nextWritePos); // Update write pointer
                channel._accessor.Flush();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CircularChannel: Error while writing data");
                return false;
            }
            finally
            {
                channel._mutex.ReleaseMutex();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }
    }
}
