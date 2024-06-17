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
        var bytesRead = base.Read(buffer, offset, count);

        if (_buffer is null)
        {
            SaveToLocalBuffer(buffer, offset, bytesRead);
        }

        return bytesRead;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _buffer is null
                   ? ReadAndSaveBufferAsync(buffer, offset, count, cancellationToken)
                   : base.ReadAsync(buffer, offset, count, cancellationToken);
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

    private async Task<int> ReadAndSaveBufferAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytesRead = await base.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        SaveToLocalBuffer(buffer, offset, bytesRead);
        return bytesRead;
    }

    private void SaveToLocalBuffer(byte[] buffer, int offset, int bytesRead)
    {
        var bufferSize = Math.Min(MaxInitialBufferSize, bytesRead);
        var localBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        // Copy from the output buffer into our saved buffer
        Array.Copy(
            sourceArray: buffer,
            sourceIndex: offset,
            destinationArray: localBuffer,
            destinationIndex: 0,
            length: bufferSize);

        _buffer = new ArraySegment<byte>(localBuffer, offset: 0, count: bufferSize);
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
