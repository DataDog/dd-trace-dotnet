// <copyright file="CircularChannel.Writer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace Datadog.Trace.Ci.Ipc;

internal partial class CircularChannel
{
    private sealed class Writer : IChannelWriter
    {
        private readonly CircularChannel _channel;

        // Mapped once and reused, so we never map a view while holding the cross-process lock.
        private readonly MemoryMappedViewAccessor _accessor;
        private long _disposed;

        internal Writer(CircularChannel channel)
        {
            _disposed = 0;
            _channel = channel;
            _accessor = channel._mmf.CreateViewAccessor();
        }

        public int GetMessageSize(in ArraySegment<byte> data) => data.Count + 2;

        public bool TryWrite(in ArraySegment<byte> data)
        {
            var channel = _channel;
            if (Interlocked.Read(ref _disposed) == 1 || Interlocked.Read(ref channel._disposed) == 1)
            {
                Log.Error("CircularChannel.Writer: Channel is disposed. Cannot write data.");
                return false;
            }

            var dataSize = GetMessageSize(in data);

            // One byte of the buffer always stays free (see the space check below)
            if (dataSize >= channel.BufferBodySize)
            {
                Log.Error("CircularChannel.Writer: Message size exceeds maximum allowed size.");
                return false;
            }

            var acquisition = channel.WaitForLock();
            if (acquisition == LockAcquisition.Abandoned)
            {
                // A previous owner died while holding the mutex. The wait still succeeded and we own the
                // mutex now, so keep going and let the finally below release it. Letting the exception
                // escape would leak ownership and stop every process from ever using this channel again.
                Log.Warning("CircularChannel.Writer: Mutex was abandoned by a previous owner. Recovering ownership.");
            }
            else if (acquisition != LockAcquisition.Acquired)
            {
                Log.Error("CircularChannel.Writer: Failed to acquire the channel lock within the time limit.");
                return false;
            }

            try
            {
                var accessor = _accessor;
                var writePos = accessor.ReadUInt16(0);
                var readPos = accessor.ReadUInt16(2);

                // Older versions marked a completely full buffer with a virtual write position past the end of the buffer
                // (BufferBodySize + writePos), because writePos == readPos means empty. That overflows the ushort once the
                // body is larger than 32 KB, so we never write it anymore (see the space check below), but we still
                // understand it in case an older version shares this channel.
                if (writePos >= channel.BufferBodySize)
                {
                    if (writePos % channel.BufferBodySize == readPos)
                    {
                        Log.Warning("CircularChannel.Writer: Buffer overflow");
                        return false;
                    }
                    else
                    {
                        // This means that the read position moved to the next buffer, so we need to reset the write position
                        writePos = (ushort)(writePos % channel.BufferBodySize);
                    }
                }

                var absoluteWritePos = HeaderSize + writePos;
                if (channel.BufferSize - absoluteWritePos < 2)
                {
                    // Not space to write the length of the message, so we need to go back to 0,
                    // but we need to check first if the read position is at the start of the buffer
                    if (readPos == 0)
                    {
                        Log.Warning("CircularChannel.Writer: Buffer overflow");
                        return false;
                    }

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

                // Always leave at least one byte free, so the write position never catches up with the read position.
                // Otherwise a full buffer would look exactly like an empty one (writePos == readPos).
                if (spaceAvailable <= dataSize)
                {
                    Log.Warning("CircularChannel.Writer: Buffer overflow");
                    return false;
                }

                // Write the first part of the data
                var remainningSpace = channel.BufferBodySize - writePos;
                var firstPartLength = Math.Min(dataSize, remainningSpace) - 2;
                accessor.Write(absoluteWritePos, (ushort)data.Count);
                if (firstPartLength > 0)
                {
                    accessor.WriteArray(absoluteWritePos + 2, data.Array!, data.Offset, firstPartLength);
                }

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
                Log.Error(ex, "CircularChannel.Writer: Error while writing data");
                return false;
            }
            finally
            {
                channel.ReleaseLock();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            // Disposed before the channel drops the memory mapped file the view came from.
            _accessor.Dispose();
        }
    }
}
