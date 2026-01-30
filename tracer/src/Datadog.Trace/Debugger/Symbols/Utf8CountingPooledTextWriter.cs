// <copyright file="Utf8CountingPooledTextWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Text;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;

namespace Datadog.Trace.Debugger.Symbols;

/// <summary>
/// A <see cref="TextWriter"/> implementation that writes into a pooled <c>char[]</c> buffer
/// while tracking the UTF-8 byte count of the written content.
/// </summary>
/// <remarks>
/// This is used to serialize JSON (via Newtonsoft) without materializing intermediate strings,
/// while keeping the byte-counting logic consistent with <see cref="Encoding.UTF8"/>.
/// </remarks>
internal sealed class Utf8CountingPooledTextWriter : TextWriter
{
    // Match Encoding.UTF8 default behavior
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    private readonly Encoder _encoder = Utf8NoBom.GetEncoder();
    private readonly char[] _singleChar = new char[1];

    private char[] _buffer;
    private int _length;
    private int _utf8ByteCount;
    private bool _disposed;

    public Utf8CountingPooledTextWriter(int initialCapacity = 1024)
    {
        if (initialCapacity <= 0)
        {
            initialCapacity = 1;
        }

        _buffer = ArrayPool<char>.Shared.Rent(initialCapacity);
    }

    public override Encoding Encoding => Utf8NoBom;

    internal int Utf8ByteCount => _utf8ByteCount;

    internal int Length => _length;

    internal char[] Buffer => _buffer;

    internal void Reset()
    {
        ThrowIfDisposed();
        _length = 0;
        _utf8ByteCount = 0;
        _encoder.Reset();
    }

    public override void Write(char value)
    {
        ThrowIfDisposed();
        EnsureCapacity(1);

        _buffer[_length++] = value;

        _singleChar[0] = value;
        _utf8ByteCount += _encoder.GetByteCount(_singleChar, 0, 1, flush: false);
    }

    public override void Write(string? value)
    {
        if (value is null)
        {
            return;
        }

        ThrowIfDisposed();
        EnsureCapacity(value.Length);

        var start = _length;
        value.CopyTo(0, _buffer, start, value.Length);
        _length += value.Length;

        _utf8ByteCount += _encoder.GetByteCount(_buffer, start, value.Length, flush: false);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        ThrowIfDisposed();

        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if ((uint)index > (uint)buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if ((uint)count > (uint)(buffer.Length - index))
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0)
        {
            return;
        }

        EnsureCapacity(count);

        Array.Copy(buffer, index, _buffer, _length, count);
        _length += count;

        _utf8ByteCount += _encoder.GetByteCount(buffer, index, count, flush: false);
    }

#if NETCOREAPP
    public override void Write(ReadOnlySpan<char> buffer)
    {
        ThrowIfDisposed();

        if (buffer.Length == 0)
        {
            return;
        }

        EnsureCapacity(buffer.Length);

        buffer.CopyTo(_buffer.AsSpan(_length));
        _utf8ByteCount += _encoder.GetByteCount(_buffer, _length, buffer.Length, flush: false);
        _length += buffer.Length;
    }
#endif

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            ArrayPool<char>.Shared.Return(_buffer);
        }

        _disposed = true;
        base.Dispose(disposing);
    }

    private void EnsureCapacity(int additionalLength)
    {
        var required = _length + additionalLength;
        if (required <= _buffer.Length)
        {
            return;
        }

        var newSize = _buffer.Length * 2;
        if (newSize < required)
        {
            newSize = required;
        }

        var newBuffer = ArrayPool<char>.Shared.Rent(newSize);
        Array.Copy(_buffer, 0, newBuffer, 0, _length);
        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = newBuffer;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(Utf8CountingPooledTextWriter));
        }
    }
}
