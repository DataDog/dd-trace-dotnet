// <copyright file="SpanCharSplitter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util;

internal readonly ref struct SpanCharSplitter
{
    private readonly ReadOnlySpan<char> _source;
    private readonly ReadOnlySpan<char> _separator;
    private readonly int _count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanCharSplitter(ReadOnlySpan<char> source, ReadOnlySpan<char> separator, int count)
    {
        if (separator.Length == 0)
        {
            throw new ArgumentException("Requires non-empty value", nameof(separator));
        }

        _source = source;
        _separator = separator;
        _count = count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanSplitEnumerator GetEnumerator() => new(_source, _separator, _count);

    internal ref struct SpanSplitEnumerator
    {
        private readonly ReadOnlySpan<char> _separator;
        private readonly ReadOnlySpan<char> _source;
        private int _nextStartIndex = 0;
        private int _count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpanSplitEnumerator(ReadOnlySpan<char> source, ReadOnlySpan<char> separator, int count)
        {
            _source = source;
            _separator = separator;
            _count = count;

            if (separator.Length == 0)
            {
                throw new ArgumentException("Requires non-empty value", nameof(separator));
            }
        }

        public SpanSplitValue Current { get; private set; }

        public bool MoveNext()
        {
            if (_nextStartIndex > _source.Length)
            {
                return false;
            }

            var nextSource = _source.Slice(_nextStartIndex);

            var foundIndex = nextSource.IndexOf(_separator);

            var length = _count > 1 && foundIndex >= 0 ? foundIndex : nextSource.Length;

            Current = new SpanSplitValue
            {
                StartIndex = _nextStartIndex,
                Length = length,
                Source = _source,
            };

            _nextStartIndex += _separator.Length + Current.Length;

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
