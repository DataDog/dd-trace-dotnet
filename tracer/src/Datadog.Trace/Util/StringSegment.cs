// <copyright file="StringSegment.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Util
{
    internal readonly struct StringSegment
    {
        public readonly string Value;
        public readonly int Offset;
        public readonly int Length;

        public StringSegment(string value)
            : this(value, offset: 0, value?.Length ?? 0)
        {
        }

        public StringSegment(string value, int offset, int length)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (offset < 0 || offset > value.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (length < 0 || length > value.Length - offset)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            Value = value;
            Offset = offset;
            Length = length;
        }

        public bool IsEmpty => Length == 0;

        public char this[int index] => index >= 0 && index < Length
                                          ? Value[Offset + index]
                                          : throw new ArgumentOutOfRangeException(nameof(index));

        public StringSegment Slice(int start)
        {
            return Slice(start, Length - start);
        }

        public StringSegment Slice(int start, int length)
        {
            if (start < 0 || start > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (length < 0 || length > Length - start)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return new StringSegment(Value, Offset + start, length);
        }

#if NETCOREAPP
        public ReadOnlySpan<char> AsSpan() => Value.AsSpan(Offset, Length);
#endif

        public StringSegment Trim()
        {
#if NETCOREAPP
            var value = AsSpan();
            var startTrimmed = value.TrimStart();
            var trimmed = startTrimmed.TrimEnd();
            return Slice(Length - startTrimmed.Length, trimmed.Length);
#else
            var start = 0;
            var end = Length;

            while (start < end && char.IsWhiteSpace(Value[Offset + start]))
            {
                start++;
            }

            while (end > start && char.IsWhiteSpace(Value[Offset + end - 1]))
            {
                end--;
            }

            return Slice(start, end - start);
#endif
        }

        public bool Equals(string? other, StringComparison comparisonType)
        {
            if (other is not { Length: var length } || length != Length)
            {
                return false;
            }

#if NETCOREAPP
            return AsSpan().Equals(other.AsSpan(), comparisonType);
#else
            return string.Compare(Value, Offset, other, 0, Length, comparisonType) == 0;
#endif
        }

        public bool StartsWith(string prefix)
        {
            if (prefix.Length > Length)
            {
                return false;
            }

#if NETCOREAPP
            return AsSpan().StartsWith(prefix.AsSpan(), StringComparison.Ordinal);
#else
            return string.Compare(Value, Offset, prefix, 0, prefix.Length, StringComparison.Ordinal) == 0;
#endif
        }

        public int IndexOf(char character, int startIndex = 0, int count = -1)
        {
            var length = count < 0 ? Length - startIndex : count;

#if NETCOREAPP
            var index = AsSpan().Slice(startIndex, length).IndexOf(character);
            return index < 0 ? -1 : startIndex + index;
#else
            var endIndex = startIndex + length;

            for (var index = startIndex; index < endIndex; index++)
            {
                if (Value[Offset + index] == character)
                {
                    return index;
                }
            }

            return -1;
#endif
        }

        public override string ToString()
        {
            return Value.Substring(Offset, Length);
        }
    }
}
