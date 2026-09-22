// <copyright file="SpanCharSplitter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util;

internal readonly ref struct SpanCharSplitter
{
    private readonly ReadOnlySpan<char> _source;
    private readonly char _separator;
    private readonly int _count;
    private readonly int _startIndex;
    private readonly int _endIndex;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanCharSplitter(string source, char separator, int count)
        : this(source is null ? throw new ArgumentNullException(nameof(source)) : source.AsSpan(), separator, count)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanCharSplitter(string source, char separator, int startIndex, int length, int count)
        : this(source is null ? throw new ArgumentNullException(nameof(source)) : source.AsSpan(), separator, startIndex, length, count)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanCharSplitter(ReadOnlySpan<char> source, char separator, int count)
        : this(source, separator, startIndex: 0, source.Length, count)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanCharSplitter(ReadOnlySpan<char> source, char separator, int startIndex, int length, int count)
    {
        _source = source;

        if ((uint)startIndex > (uint)source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }

        if ((uint)length > (uint)(source.Length - startIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        _separator = separator;
        _count = count;
        _startIndex = startIndex;
        _endIndex = startIndex + length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanSplitEnumerator GetEnumerator() => new(_source, _separator, _startIndex, _endIndex, _count);

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref struct SpanSplitEnumerator(ReadOnlySpan<char> source, char separator, int startIndex, int endIndex, int count)
    {
        private readonly char _separator = separator;
        private readonly ReadOnlySpan<char> _source = source;
        private readonly int _endIndex = endIndex;
        private int _nextStartIndex = startIndex;
        private int _count = count;

        public SpanSplitValue Current { get; private set; }

        public bool MoveNext()
        {
            if (_nextStartIndex > _endIndex)
            {
                return false;
            }

            var remainder = _source.Slice(_nextStartIndex, _endIndex - _nextStartIndex);
            var foundIndex = remainder.IndexOf(_separator);

            var length = _count > 1 && foundIndex >= 0
                             ? foundIndex
                             : _endIndex - _nextStartIndex;

            Current = new SpanSplitValue
            {
                StartIndex = _nextStartIndex,
                Length = length,
                Source = _source
            };

            _nextStartIndex += Current.Length + 1;

            _count -= 1;

            return true;
        }

        public readonly ref struct SpanSplitValue
        {
            public int StartIndex { get; init; }

            public int Length { get; init; }

            public ReadOnlySpan<char> Source { get; init; }

            public static implicit operator ReadOnlySpan<char>(SpanSplitValue value) => value.AsSpan();

            public ReadOnlySpan<char> AsSpan() => Source.Slice(StartIndex, Length);
        }
    }
}
