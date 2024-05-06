// <copyright file="CircularChannel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
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

    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly Mutex _mutex;
    private readonly Timer _pollingTimer;

    public CircularChannel(string fileName)
    {
        _mmf = MemoryMappedFile.CreateFromFile(fileName, FileMode.OpenOrCreate, "CircularBuffer", BufferSize);
        _accessor = _mmf.CreateViewAccessor();
        _mutex = new Mutex(false, $"{fileName}.mutex");
        InitializeHeader();
        _pollingTimer = new Timer(PollForMessages, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(PollingInterval));
    }

    public event EventHandler<byte[]>? MessageReceived;

    private void InitializeHeader()
    {
        _mutex.WaitOne();
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
        _mutex.WaitOne();
        try
        {
            var writePos = _accessor.ReadInt32(0);
            var readPos = _accessor.ReadInt32(4);
            while (readPos != writePos)
            {
                var length = _accessor.ReadInt32(HeaderSize + readPos);
                var data = new byte[length];
                _accessor.ReadArray(HeaderSize + readPos + 4, data, 0, length);

                var nextReadPos = (readPos + length + 4) % MaxMessageSize;
                _accessor.Write(4, nextReadPos); // Update read pointer

                MessageReceived?.Invoke(this, data);

                readPos = nextReadPos; // Update local readPos to continue reading if more data is available
            }
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }

    public void Write(byte[] data)
    {
        if (data.Length > MaxMessageSize - HeaderSize)
        {
            throw new ArgumentException("Data too large for the message buffer", nameof(data));
        }

        _mutex.WaitOne();
        try
        {
            var writePos = _accessor.ReadInt32(0);
            var readPos = _accessor.ReadInt32(4);
            var nextWritePos = (writePos + data.Length + 4) % MaxMessageSize;

            // Check for buffer overflow condition:
            // Ensure that if the write position is behind the read position (writePos < readPos),
            // the next write position after writing the data (nextWritePos) does not surpass
            // the read position, potentially overwriting unread data.
            if (writePos < readPos && nextWritePos > readPos)
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

    public void Close()
    {
        _pollingTimer.Dispose();
        _accessor.Dispose();
        _mmf.Dispose();
        _mutex.Dispose();
    }

    public void Dispose()
    {
        Close();
    }
}
