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

namespace Datadog.Trace.Ci.Ipc;

internal class CircularChannel : IDisposable
{
    private const int BufferSize = 4096;
    private const int HeaderSize = 8;
    private const int MaxMessageSize = BufferSize - HeaderSize;
    private const int PollingInterval = 500;
    private const int MutexTimeout = 10000;

    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly Mutex _mutex;
    private readonly Timer _pollingTimer;
    private bool _disposed;

    public CircularChannel(string fileName)
    {
        var fileInfo = new FileInfo(fileName);
        if (fileInfo is { Exists: true, Length: < BufferSize })
        {
            // Resize file if it exists but is too small
            using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Write, FileShare.None);
            stream.SetLength(BufferSize);
        }

        _mmf = MemoryMappedFile.CreateFromFile(fileName, FileMode.OpenOrCreate, "CircularBuffer", BufferSize);
        _accessor = _mmf.CreateViewAccessor();
        _mutex = new Mutex(false, $"{Path.GetFileNameWithoutExtension(fileName)}.mutex");
        if (!fileInfo.Exists)
        {
            // Initialize only if the file did not exist prior
            InitializeHeader();
        }

        _pollingTimer = new Timer(PollForMessages, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(PollingInterval));
    }

    public event EventHandler<byte[]>? MessageReceived;

    private void InitializeHeader()
    {
        var hasHandle = _mutex.WaitOne(MutexTimeout);
        if (!hasHandle)
        {
            throw new TimeoutException("Failed to acquire mutex within the time limit.");
        }

        try
        {
            _accessor.Write(0, 0); // Write pointer
            _accessor.Write(4, 0); // Read pointer
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }

    private void PollForMessages(object? state)
    {
        var hasHandle = _mutex.WaitOne(MutexTimeout);
        if (!hasHandle)
        {
            CIVisibility.Log.Error("Failed to acquire mutex within the time limit.");
            return;
        }

        List<byte[]>? messagesToHandle = null;
        try
        {
            var writePos = _accessor.ReadInt32(0);
            var readPos = _accessor.ReadInt32(4);
            while (readPos != writePos)
            {
                messagesToHandle ??= new List<byte[]>();

                var length = _accessor.ReadInt32(HeaderSize + readPos);
                // Simple sanity check
                if (length is < 0 or > MaxMessageSize)
                {
                    // Handle error, reset pointers, or skip
                    break;
                }

                var data = new byte[length];
                _accessor.ReadArray(HeaderSize + readPos + 4, data, 0, length);

                var nextReadPos = (readPos + length + 4) % MaxMessageSize;
                _accessor.Write(4, nextReadPos); // Update read pointer

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
            _mutex.ReleaseMutex();
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

    public void Write(byte[] data)
    {
        if (data.Length > MaxMessageSize - HeaderSize)
        {
            throw new ArgumentException("Data too large for the message buffer", nameof(data));
        }

        var hasHandle = _mutex.WaitOne(MutexTimeout);
        if (!hasHandle)
        {
            throw new TimeoutException("Failed to acquire mutex within the time limit.");
        }

        try
        {
            var writePos = _accessor.ReadInt32(0);
            var readPos = _accessor.ReadInt32(4);
            var nextWritePos = (writePos + data.Length + 4) % MaxMessageSize;

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
                throw new InvalidOperationException("Buffer overflow");
            }

            _accessor.Write(HeaderSize + writePos, data.Length);
            _accessor.WriteArray(HeaderSize + writePos + 4, data, 0, data.Length);
            _accessor.Write(0, nextWritePos); // Update write pointer
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _pollingTimer.Dispose();
        _accessor.Dispose();
        _mmf.Dispose();
        _mutex.Dispose();
        _disposed = true;
    }
}
