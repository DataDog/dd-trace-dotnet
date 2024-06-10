// <copyright file="InitiallyBufferedStream.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;

namespace Datadog.Trace.Util.Streams;

/// <summary>
/// A stream that only buffers a portion of the initial stream in memory, so that it can be
/// retrieved again later if needed (for example if deserialization fails)
/// </summary>
internal class InitiallyBufferedStream(Stream innerStream) : LeaveOpenDelegatingStream(innerStream)
{
    internal const int MaxInitialBufferSize = 128;

    private ArraySegment<byte>? _buffer = null;
    private int _offset = 0;

    public string? GetBufferedContent()
    {
        try
        {
            if (_buffer is not null)
            {
                return System.Text.Encoding.UTF8.GetString(_buffer.Value.Array!, _buffer.Value.Offset, _buffer.Value.Count);
            }
        }
        catch (Exception)
        {
            // Maybe not valid UTF-8, just swallow
        }

        return null;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_offset >= _buffer?.Count)
        {
            // already "used up" the local buffer
            return base.Read(buffer, offset, count);
        }

        if (_buffer is null)
        {
            // First read, populate the temp buffer
            var bufferSize = Math.Min(MaxInitialBufferSize, count);
            var localBuffer = GetBuffer(bufferSize);
            var bytesRead = base.Read(localBuffer, offset: 0, count: bufferSize);
            _buffer = new ArraySegment<byte>(localBuffer, offset: 0, count: bytesRead);
        }

        // Copy from the temp buffer to the output
        var bytesToCopy = Math.Min(count, _buffer.Value.Count - _offset);
        Array.Copy(
            sourceArray: _buffer.Value.Array!,
            sourceIndex: _offset,
            destinationArray: buffer!,
            destinationIndex: offset,
            length: bytesToCopy);

        _offset += bytesToCopy;
        return bytesToCopy;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_offset >= _buffer?.Count)
        {
            // already "used up" the local buffer
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        return PopulateBufferAsync();

        async Task<int> PopulateBufferAsync()
        {
            if (_buffer is null)
            {
                var bufferSize = Math.Min(MaxInitialBufferSize, count);
                var localBuffer = GetBuffer(bufferSize);
                var bytesRead = await base.ReadAsync(localBuffer, offset: 0, count: bufferSize, cancellationToken).ConfigureAwait(false);
                _buffer = new ArraySegment<byte>(localBuffer, offset: 0, count: bytesRead);
            }

            var bytesToCopy = Math.Min(count, _buffer.Value.Count - _offset);
            Array.Copy(
                sourceArray: _buffer.Value.Array!,
                sourceIndex: _offset,
                destinationArray: buffer!,
                destinationIndex: offset,
                length: bytesToCopy);

            _offset += bytesToCopy;
            return bytesToCopy;
        }
    }

    protected override void Dispose(bool disposing)
    {
        ReturnBuffer();
        base.Dispose(disposing);
    }

#if NETCOREAPP
    public override ValueTask DisposeAsync()
    {
        ReturnBuffer();
        return base.DisposeAsync();
    }
#endif

    private byte[] GetBuffer(int size)
    {
        return ArrayPool<byte>.Shared.Rent(size);
    }

    private void ReturnBuffer()
    {
        if (_buffer?.Array is { } buffer)
        {
            _buffer = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
