// <copyright file="CircularChannel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci.Ipc;

internal class CircularChannel : IDisposable
{
    private const int BufferSize = 65536;
    private const int HeaderSize = 4;
    private const int BufferBodySize = BufferSize - HeaderSize;

    private const int PollingInterval = 250;
    private const int MutexTimeout = 10000;

    internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CircularChannel));

    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly Mutex _mutex;
    private bool _disposed;

    private Writer? _writer;
    private Receiver? _receiver;

    public CircularChannel(string fileName)
    {
        var fileInfo = new FileInfo(fileName);
        if (fileInfo is { Exists: true, Length: < BufferSize })
        {
            // Resize file if it exists but is too small
            using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Write, FileShare.None);
            stream.SetLength(BufferSize);
        }

        _mmf = MemoryMappedFile.CreateFromFile(fileName, FileMode.OpenOrCreate, null, BufferSize);
        _accessor = _mmf.CreateViewAccessor();
        _mutex = new Mutex(false, $"{Path.GetFileNameWithoutExtension(fileName)}.mutex");
        InitializeHeader();
    }

    private void InitializeHeader()
    {
        var hasHandle = _mutex.WaitOne(MutexTimeout);
        if (!hasHandle)
        {
            throw new TimeoutException("Failed to acquire mutex within the time limit.");
        }

        try
        {
            _accessor.Write(0, (ushort)0); // Write pointer
            _accessor.Write(2, (ushort)0); // Read pointer
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }

    public Receiver GetReceiver()
    {
        return _receiver ??= new Receiver(this);
    }

    public Writer GetWriter()
    {
        return _writer ??= new Writer(this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _writer?.Dispose();
        _receiver?.Dispose();
        _accessor.Dispose();
        _mmf.Dispose();
        _mutex.Dispose();
        _disposed = true;
    }

    public class Receiver : IDisposable
    {
        private readonly Timer _pollingTimer;
        private readonly CircularChannel _channel;
        private bool _disposed;

        public Receiver(CircularChannel channel)
        {
            _disposed = false;
            _channel = channel;
            _pollingTimer = new Timer(PollForMessages, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(PollingInterval));
        }

        public event EventHandler<byte[]>? MessageReceived;

        private void PollForMessages(object? state)
        {
            var channel = _channel;
            if (channel._disposed)
            {
                return;
            }

            var hasHandle = channel._mutex.WaitOne(MutexTimeout);
            if (!hasHandle)
            {
                CIVisibility.Log.Error("Failed to acquire mutex within the time limit.");
                return;
            }

            List<byte[]>? messagesToHandle = null;
            try
            {
                var writePos = channel._accessor.ReadUInt16(0);
                var readPos = channel._accessor.ReadUInt16(2);
                while (readPos != writePos)
                {
                    messagesToHandle ??= new List<byte[]>();

                    var absoluteReadPos = HeaderSize + readPos;
                    if (BufferSize - absoluteReadPos < 2)
                    {
                        // Not space to read the length of the message, so we need to go back to 0
                        readPos = 0;
                        absoluteReadPos = HeaderSize;
                    }

                    var length = channel._accessor.ReadUInt16(absoluteReadPos);

                    // Simple sanity check
                    if (length + 2 > BufferBodySize)
                    {
                        // Handle error, reset pointers, or skip
                        break;
                    }

                    var data = new byte[length];

                    // Read the first part of the data
                    var firstPartLength = Math.Min(length, BufferSize - absoluteReadPos - 2);
                    channel._accessor.ReadArray(absoluteReadPos + 2, data, 0, firstPartLength);

                    // Read the second part of the data, if any, from the start of the buffer
                    var secondPartLength = length - firstPartLength;
                    if (secondPartLength > 0)
                    {
                        channel._accessor.ReadArray(HeaderSize, data, firstPartLength, secondPartLength);
                    }

                    var nextReadPos = (ushort)((readPos + length + 2) % BufferBodySize);
                    channel._accessor.Write(2, nextReadPos); // Update read pointer

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
                        CIVisibility.Log.Error(ex, "Error during message event handling.");
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _pollingTimer.Dispose();
            _disposed = true;
        }
    }

    public class Writer : IDisposable
    {
        private readonly CircularChannel _channel;
        private bool _disposed;

        internal Writer(CircularChannel channel)
        {
            _disposed = false;
            _channel = channel;
        }

        internal int GetMessageSize(byte[] data) => data.Length + 2;

        public bool TryWrite(byte[] data)
        {
            var channel = _channel;
            if (channel._disposed)
            {
                Log.Error("CircularChannel: Channel is disposed. Cannot write data.");
                return false;
            }

            var dataSize = GetMessageSize(data);
            if (dataSize > BufferBodySize)
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
                if (BufferSize - absoluteWritePos < 2)
                {
                    // Not space to write the length of the message, so we need to go back to 0
                    writePos = 0;
                    absoluteWritePos = HeaderSize;
                }

                var nextWritePos = (ushort)((writePos + dataSize) % BufferBodySize);

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
                var remainningSpace = BufferBodySize - writePos;
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
