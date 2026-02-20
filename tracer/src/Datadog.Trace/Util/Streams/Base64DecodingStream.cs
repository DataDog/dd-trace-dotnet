// <copyright file="Base64DecodingStream.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Util.Streams;

/// <summary>
/// A read-only, forward-only <see cref="Stream"/> that decodes a base64-encoded <see cref="string"/>
/// into its raw bytes on-the-fly, without allocating the full decoded byte array.
/// </summary>
/// <remarks>
/// This stream processes base64 input in chunks, decoding only as much as
/// the caller requests via <see cref="Read(byte[], int, int)"/>.
/// The input must be valid base64 with standard padding (no embedded whitespace).
/// On .NET Core, decodes directly into the caller's buffer with no intermediate allocation.
/// On .NET Framework, uses <see cref="ArrayPool{T}"/> internally to avoid GC pressure.
/// </remarks>
internal sealed class Base64DecodingStream : Stream
{
#if !NETCOREAPP
    /// <summary>
    /// Maximum number of base64 characters to process per decode operation.
    /// Must be a multiple of 4 (the base64 quantum size).
    /// The internal buffer also holds the narrowed ASCII bytes before
    /// in-place decode, so the buffer size equals this value.
    /// </summary>
    private const int CharsPerChunk = 4096;
#endif

    private readonly string _base64;
    private int _charPosition;

#if !NETCOREAPP
    private byte[] _buffer;
    private int _bufferOffset;
    private int _bufferCount;
#endif

    public Base64DecodingStream(string base64)
    {
        _base64 = base64 ?? throw new ArgumentNullException(nameof(base64));
#if !NETCOREAPP
        _buffer = ArrayPool<byte>.Shared.Rent(CharsPerChunk);
#endif
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

#if NETCOREAPP
    public override int Read(byte[] buffer, int offset, int count)
        => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> destination)
    {
        if (destination.Length == 0 || _charPosition >= _base64.Length)
        {
            return 0;
        }

        var remainingChars = _base64.Length - _charPosition;
        var maxQuanta = destination.Length / 3;

        if (maxQuanta == 0)
        {
            ThrowDestinationTooSmall();
        }

        var charsToProcess = maxQuanta * 4;
        if (charsToProcess >= remainingChars)
        {
            charsToProcess = remainingChars;
        }

        if (!Convert.TryFromBase64Chars(
                _base64.AsSpan(_charPosition, charsToProcess),
                destination,
                out var bytesWritten))
        {
            ThrowFormatException();
        }

        _charPosition += charsToProcess;
        return bytesWritten;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return cancellationToken.IsCancellationRequested
                   ? new ValueTask<int>(Task.FromCanceled<int>(cancellationToken))
                   : new ValueTask<int>(Read(buffer.Span));
    }
#else
    public override int Read(byte[] buffer, int offset, int count)
    {
        var totalRead = 0;

        while (count > 0)
        {
            // Drain any previously decoded bytes still in the internal buffer
            var available = _bufferCount - _bufferOffset;
            if (available > 0)
            {
                var toCopy = Math.Min(count, available);
                Buffer.BlockCopy(_buffer, _bufferOffset, buffer, offset, toCopy);
                _bufferOffset += toCopy;
                offset += toCopy;
                count -= toCopy;
                totalRead += toCopy;
                continue;
            }

            // No buffered bytes remain — decode another chunk from the input string
            if (_charPosition >= _base64.Length)
            {
                break;
            }

            DecodeNextChunk();
        }

        return totalRead;
    }
#endif

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return cancellationToken.IsCancellationRequested
                   ? Task.FromCanceled<int>(cancellationToken)
                   : Task.FromResult(Read(buffer, offset, count));
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
#if !NETCOREAPP
        if (_buffer is { } buffer)
        {
            _buffer = null!;
            ArrayPool<byte>.Shared.Return(buffer);
        }
#endif

        base.Dispose(disposing);
    }

    [DoesNotReturn]
    private static void ThrowFormatException() => throw new FormatException("The input is not a valid base64 string.");

    [DoesNotReturn]
    private static void ThrowDestinationTooSmall() => throw new ArgumentException("Destination buffer must be at least 3 bytes to hold a decoded base64 quantum.");

#if !NETCOREAPP
    private void DecodeNextChunk()
    {
        var remainingChars = _base64.Length - _charPosition;
        var charsToProcess = Math.Min(remainingChars, CharsPerChunk);

        // Round down to a multiple of 4 for non-final chunks.
        // Base64 decoding operates on 4-character quanta; the final chunk
        // may include padding characters and is always a valid quantum boundary.
        if (charsToProcess < remainingChars)
        {
            charsToProcess &= ~3;
        }

        if (charsToProcess == 0)
        {
            // Remaining chars < 4 and this is not the final chunk — shouldn't happen
            // for valid base64, but guard against infinite loops.
            _charPosition = _base64.Length;
            return;
        }

        // All valid base64 characters are in the ASCII range (0–127),
        // so we can safely narrow each char to a byte for the UTF-8 decoder.
        var buf = _buffer;
        var str = _base64;
        var offset = _charPosition;

        for (var i = 0; i < charsToProcess; i++)
        {
            buf[i] = (byte)str[offset + i];
        }

        // Decode the UTF-8 base64 bytes in-place. The decoded output is always
        // shorter than the input (3 bytes per 4 input bytes), so in-place is safe.
        var status = Base64.DecodeFromUtf8InPlace(buf.AsSpan(0, charsToProcess), out var bytesWritten);
        if (status != OperationStatus.Done)
        {
            ThrowFormatException();
        }

        _bufferOffset = 0;
        _bufferCount = bytesWritten;
        _charPosition += charsToProcess;
    }
#endif
}
