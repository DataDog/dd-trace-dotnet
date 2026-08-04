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

        public bool Equals(string? other, StringComparison comparisonType)
        {
            return other is { Length: var length } &&
                   length == Length &&
                   string.Compare(Value, Offset, other, 0, Length, comparisonType) == 0;
        }

        public override string ToString()
        {
            return Value.Substring(Offset, Length);
        }
    }
}
