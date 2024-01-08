// <copyright file="ChunkedEncodingWriteStream.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Util.Streams;

namespace Datadog.Trace.HttpOverStreams;

internal class ChunkedEncodingWriteStream(Stream innerStream) : LeaveOpenDelegatingStream(innerStream)
{
    private static readonly byte[] CrLfBytes = { 0x0D, 0x0A }; // UTF-8 for \r\n
    private static readonly byte[] FinalChunkBytes = { 0x30, 0x0D, 0x0A, 0x0D, 0x0A, }; // UTF-8 for 0\r\n\r\n

    private readonly Stream _innerStream = innerStream;
    private readonly byte[] _chunkSizeBuffer = new byte[8]; // max length of Int32 as UTF-8 hex

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        // don't want to ever send a zero-length terminator unless we're at the end
        if (count == 0)
        {
            // Don't write if nothing was given, especially since we don't want to accidentally send a 0 chunk,
            // which would indicate end of body.  Instead, just ensure no content is stuck in the buffer.
            await _innerStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // write the chunked encoding header
        var bytesWritten = WriteChunkedEncodingHeaderToBuffer(_chunkSizeBuffer, count);
        await _innerStream.WriteAsync(_chunkSizeBuffer, 0, bytesWritten, cancellationToken).ConfigureAwait(false);

        // add the new line
        await _innerStream.WriteAsync(CrLfBytes, offset: 0, count: 2, cancellationToken).ConfigureAwait(false);

        // add the content
        await _innerStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);

        // add the extra new line
        await _innerStream.WriteAsync(CrLfBytes, offset: 0, count: 2, cancellationToken).ConfigureAwait(false);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        // don't want to ever send a zero-length terminator unless we're at the end
        if (count == 0)
        {
            // Don't write if nothing was given, especially since we don't want to accidentally send a 0 chunk,
            // which would indicate end of body.  Instead, just ensure no content is stuck in the buffer.
            _innerStream.Flush();
            return;
        }

        // write the chunked encoding header
        var bytesWritten = WriteChunkedEncodingHeaderToBuffer(_chunkSizeBuffer, count);
        _innerStream.Write(_chunkSizeBuffer, 0, bytesWritten);

        // add the new line
        _innerStream.Write(CrLfBytes, offset: 0, count: 2);

        // flush the content
        _innerStream.Write(buffer, offset, count);

        // add the extra new line
        _innerStream.Write(CrLfBytes, offset: 0, count: 2);
    }

    public Task FinishAsync()
    {
        // Send 0 byte chunk to indicate end, then final CrLf
        return _innerStream.WriteAsync(FinalChunkBytes, 0, FinalChunkBytes.Length);
    }

    internal static int WriteChunkedEncodingHeaderToBuffer(byte[] outputBuffer, int count)
    {
#if NET6_0_OR_GREATER
        // Try to format into our output buffer directly.
        if (System.Buffers.Text.Utf8Formatter.TryFormat(count, outputBuffer, out var bytesWritten, 'X'))
        {
            return bytesWritten;
        }
#endif

        var hexEncoded = count.ToString("X", CultureInfo.InvariantCulture);
        // Assuming there's enough space in the buffer
        // As this is ascii, the `char`s can be directly converted to bytes and it's valid UTF-8
        for (var i = 0; i < hexEncoded.Length; i++)
        {
            outputBuffer[i] = (byte)hexEncoded[i];
        }

        return hexEncoded.Length;
    }
}
