// <copyright file="ValueStringBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
#nullable enable
#pragma warning disable SA1201
#pragma warning disable SA1623

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Util;

// Based on:
// https://source.dot.net/#System.Diagnostics.Process/src/libraries/Common/src/System/Text/ValueStringBuilder.cs,157e1a7ce4de87da
internal unsafe ref struct ValueStringBuilder
{
    private readonly void* _charsPtr;
    private readonly int _length;
    private char[]? _arrayToReturnToPool;
    private Span<char> _backChars;
    private int _pos;

    public ValueStringBuilder(void* ptr, int length)
    {
        _charsPtr = ptr;
        _length = length;
        _pos = 0;
    }

    public ValueStringBuilder(Span<char> initialBuffer)
    {
        _arrayToReturnToPool = null;
        _backChars = initialBuffer;
        _pos = 0;
    }

    public ValueStringBuilder(int initialCapacity)
    {
        _arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
        _backChars = _arrayToReturnToPool;
        _pos = 0;
    }

    public int Length
    {
        get => _pos;
        set => _pos = value;
    }

    private Span<char> Chars
    {
        get
        {
            if (!_backChars.IsEmpty)
            {
                return _backChars;
            }

            return _charsPtr != null ? new Span<char>(_charsPtr, _length) : Span<char>.Empty;
        }
    }

    public int Capacity
    {
        get
        {
            if (!_backChars.IsEmpty)
            {
                return _backChars.Length;
            }

            return _charsPtr != null ? _length : 0;
        }
    }

    public void EnsureCapacity(int capacity)
    {
        // If the caller has a bug and calls this with negative capacity, make sure to call Grow to throw an exception.
        if ((uint)capacity > (uint)Capacity)
        {
            Grow(capacity - _pos);
        }
    }

    /// <summary>
    /// Get a pinnable reference to the builder.
    /// Does not ensure there is a null char after <see cref="Length"/>
    /// This overload is pattern matched in the C# 7.3+ compiler so you can omit
    /// the explicit method call, and write eg "fixed (char* c = builder)"
    /// </summary>
    public ref char GetPinnableReference()
    {
        return ref MemoryMarshal.GetReference(Chars);
    }

    /// <summary>
    /// Get a pinnable reference to the builder.
    /// </summary>
    /// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
    public ref char GetPinnableReference(bool terminate)
    {
        if (terminate)
        {
            EnsureCapacity(Length + 1);
            Chars[Length] = '\0';
        }

        return ref MemoryMarshal.GetReference(Chars);
    }

    public ref char this[int index]
    {
        get
        {
            return ref Chars[index];
        }
    }

    public override string ToString()
    {
        var s = Chars.Slice(0, _pos).ToString();
        Dispose();
        return s;
    }

    /// <summary>Returns the underlying storage of the builder.</summary>
    public Span<char> RawChars => Chars;

    /// <summary>
    /// Returns a span around the contents of the builder.
    /// </summary>
    /// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
    public ReadOnlySpan<char> AsSpan(bool terminate)
    {
        if (terminate)
        {
            EnsureCapacity(Length + 1);
            Chars[Length] = '\0';
        }

        return Chars.Slice(0, _pos);
    }

    public ReadOnlySpan<char> AsSpan() => Chars.Slice(0, _pos);

    public ReadOnlySpan<char> AsSpan(int start) => Chars.Slice(start, _pos - start);

    public ReadOnlySpan<char> AsSpan(int start, int length) => Chars.Slice(start, length);

    public bool TryCopyTo(Span<char> destination, out int charsWritten)
    {
        if (Chars.Slice(0, _pos).TryCopyTo(destination))
        {
            charsWritten = _pos;
            Dispose();
            return true;
        }

        charsWritten = 0;
        Dispose();
        return false;
    }

    public void Insert(int index, char value, int count)
    {
        var chars = Chars;
        if (_pos > chars.Length - count)
        {
            Grow(count);
            chars = Chars;
        }

        var remaining = _pos - index;
        chars.Slice(index, remaining).CopyTo(chars.Slice(index + count));
        chars.Slice(index, count).Fill(value);
        _pos += count;
    }

    public void Insert(int index, string? s)
    {
        if (s == null)
        {
            return;
        }

        var count = s.Length;
        var chars = Chars;

        if (_pos > (chars.Length - count))
        {
            Grow(count);
            chars = Chars;
        }

        var remaining = _pos - index;
        chars.Slice(index, remaining).CopyTo(chars.Slice(index + count));
        s.AsSpan().CopyTo(chars.Slice(index));
        _pos += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c)
    {
        var pos = _pos;
        var chars = Chars;
        if ((uint)pos < (uint)chars.Length)
        {
            chars[pos] = c;
            _pos = pos + 1;
        }
        else
        {
            GrowAndAppend(c);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(string? s)
    {
        if (s == null)
        {
            return;
        }

        var pos = _pos;
        var chars = Chars;

        // very common case, e.g. appending strings from NumberFormatInfo like separators, percent symbols, etc.
        if (s.Length == 1 && (uint)pos < (uint)chars.Length)
        {
            chars[pos] = s[0];
            _pos = pos + 1;
        }
        else
        {
            AppendSlow(s);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine(string? s)
    {
        Append(s);
        Append(Environment.NewLine);
    }

    private void AppendSlow(string s)
    {
        var pos = _pos;
        var chars = Chars;
        if (pos > chars.Length - s.Length)
        {
            Grow(s.Length);
            chars = Chars;
        }

        s.AsSpan().CopyTo(chars.Slice(pos));
        _pos += s.Length;
    }

    public void Append(char c, int count)
    {
        var chars = Chars;
        if (_pos > chars.Length - count)
        {
            Grow(count);
            chars = Chars;
        }

        var dst = chars.Slice(_pos, count);
        for (var i = 0; i < dst.Length; i++)
        {
            dst[i] = c;
        }

        _pos += count;
    }

    public void Append(char* value, int length)
    {
        var chars = Chars;
        var pos = _pos;
        if (pos > chars.Length - length)
        {
            Grow(length);
            chars = Chars;
        }

        var dst = chars.Slice(_pos, length);
        for (var i = 0; i < dst.Length; i++)
        {
            dst[i] = *value++;
        }

        _pos += length;
    }

    public void Append(ReadOnlySpan<char> value)
    {
        var pos = _pos;
        var chars = Chars;
        if (pos > chars.Length - value.Length)
        {
            Grow(value.Length);
            chars = Chars;
        }

        value.CopyTo(chars.Slice(_pos));
        _pos += value.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<char> AppendSpan(int length)
    {
        var origPos = _pos;
        var chars = Chars;
        if (origPos > chars.Length - length)
        {
            Grow(length);
            chars = Chars;
        }

        _pos = origPos + length;
        return chars.Slice(origPos, length);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAppend(char c)
    {
        Grow(1);
        Append(c);
    }

    /// <summary>
    /// Resize the internal buffer either by doubling current buffer size or
    /// by adding <paramref name="additionalCapacityBeyondPos"/> to
    /// <see cref="_pos"/> whichever is greater.
    /// </summary>
    /// <param name="additionalCapacityBeyondPos">
    /// Number of chars requested beyond current position.
    /// </param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int additionalCapacityBeyondPos)
    {
        const uint ArrayMaxLength = 0x7FFFFFC7; // same as Array.MaxLength
        var chars = Chars;

        // Increase to at least the required size (_pos + additionalCapacityBeyondPos), but try
        // to double the size if possible, bounding the doubling to not go beyond the max array length.
        var newCapacity = (int)Math.Max(
            (uint)(_pos + additionalCapacityBeyondPos),
            Math.Min((uint)chars.Length * 2, ArrayMaxLength));

        // Make sure to let Rent throw an exception if the caller has a bug and the desired capacity is negative.
        // This could also go negative if the actual required length wraps around.
        var poolArray = ArrayPool<char>.Shared.Rent(newCapacity);

        chars.Slice(0, _pos).CopyTo(poolArray);

        var toReturn = _arrayToReturnToPool;
        _backChars = _arrayToReturnToPool = poolArray;
        if (toReturn != null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        var toReturn = _arrayToReturnToPool;
        this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
        if (toReturn != null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }
}
#endif
