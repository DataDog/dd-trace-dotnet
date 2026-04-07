// <copyright file="SpanCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Agent;

/// <summary>
/// Represents zero/null, one, or many Spans in an efficient way.
/// </summary>
internal readonly struct SpanCollection : IEnumerable<Span>
{
    private readonly object? _values;
    public readonly int Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanCollection"/> structure using the specified Span.
    /// </summary>
    /// <param name="value">The span to include in the collection.</param>
    public SpanCollection(Span value)
    {
        _values = value;
        Count = 1;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanCollection"/> structure using the specified capacity, but no spans.
    /// </summary>
    /// <param name="arrayBuilderCapacity">The value to initializer</param>
    public SpanCollection(int arrayBuilderCapacity)
    {
        _values = new Span[arrayBuilderCapacity];
        Count = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanCollection"/> structure using the specified array of Spans.
    /// </summary>
    public SpanCollection(Span[] values, int count)
    {
        // We assume that the caller is "sensible" here, and doesn't set count > values.Length,
        // but that will get hit "safely" elsewhere if it happens
        _values = values;
        Count = count;
    }

    [TestingOnly]
    internal SpanCollection(Span[] values)
        : this(values, values.Length)
    {
    }

    /// <summary>
    /// Gets the first span in the <see cref="SpanCollection" />, or returns null if the collection is empty
    /// </summary>
    public Span? FirstSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // Take local copy of _values so type checks remain valid even if the SpanCollection is overwritten in memory
            var value = _values;
            if (value is Span span)
            {
                return span;
            }

            if (value is null)
            {
                return null;
            }

            // Not Span, not null, can only be SpanArray
            return Unsafe.As<Span[]>(value)[0];
        }
    }

    /// <summary>
    /// Gets the <see cref="Span"/> at the specified index.
    /// </summary>
    /// <value>The Span at the specified index.</value>
    /// <param name="index">The zero-based index of the element to get.</param>
    public Span this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // Take local copy of _values so type checks remain valid even if the SpanCollection is overwritten in memory
            object? value = _values;
            if (index < Count)
            {
                if (value is Span str)
                {
                    return str;
                }
                else if (value != null)
                {
                    // Not Span, not null, can only be Span[]
                    return Unsafe.As<Span[]>(value)[index];
                }
            }

            return OutOfBounds(); // throws
        }
    }

    /// <summary>
    /// Concatenates specified instance of <see cref="SpanCollection"/> with specified <see cref="Span"/>.
    /// </summary>
    /// <param name="values">The <see cref="SpanCollection"/> to add ti.</param>
    /// <param name="value">The <see cref="Span" /> to add.</param>
    /// <returns>The concatenation of <paramref name="values"/> and <paramref name="value"/>.</returns>
    public static SpanCollection Append(in SpanCollection values, Span value)
    {
        // Take local copy of _values so type checks remain valid even if the SpanCollection is overwritten in memory
        var current = values._values;
        if (current is null)
        {
            return new SpanCollection(value);
        }

        if (current is Span span)
        {
            // We use a default capacity of 4 spans
            // 2 Spans would cover 25% not covered by single span case, 4 covers ~ 70%, 8 covers ~92%
            return new SpanCollection([span, value, null!, null!], 2);
        }

        // Not Span, not null, can only be Span[], so add the span
        var array = Unsafe.As<Span[]>(current);
        array = GrowIfNeeded(array, values.Count);
        array[values.Count] = value;
        return new SpanCollection(array, values.Count + 1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Span OutOfBounds()
    {
        return Array.Empty<Span>()[0]; // throws
    }

    /// <summary>
    /// Try to get the underlying Span array from the current <see cref="SpanCollection"/> object as an `ArraySegment`.
    /// If the <see cref="SpanCollection"/> does _not_ contain an array (because it contains 0 or 1 spans) then returns
    /// <c>null</c>.
    /// </summary>
    /// <returns>A wrapper around the Span array represented by this instance if this instance represents more than
    /// one span, otherwise null.</returns>
    public ArraySegment<Span>? TryGetArray()
    {
        // Take local copy of _values so type checks remain valid even if the SpanCollection is overwritten in memory
        object? value = _values;
        if (value is Span[] values)
        {
            return new ArraySegment<Span>(values, 0, Count);
        }

        // zero or one spans, so return null
        return null;
    }

    private static Span[] GrowIfNeeded(Span[] array, int currentCount)
    {
        if (currentCount < array.Length)
        {
            // The array is already big enough
            return array;
        }

        var newArray = new Span[array.Length * 2];

        Array.Copy(array, 0, newArray, 0, array.Length);

        return newArray;
    }

    /// <summary>Retrieves an object that can iterate through the individual Spans in this <see cref="SpanCollection" />.</summary>
    /// <returns>An enumerator that can be used to iterate through the <see cref="SpanCollection" />.</returns>
    public Enumerator GetEnumerator()
    {
        return new Enumerator(_values, Count);
    }

    /// <inheritdoc cref="GetEnumerator()" />
    IEnumerator<Span> IEnumerable<Span>.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc cref="GetEnumerator()" />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Enumerates the Span values of a <see cref="SpanCollection" />.
    /// </summary>
    public struct Enumerator : IEnumerator<Span>
    {
        private readonly Span[]? _values;
        private readonly int _count;
        private int _index;
        private Span? _current;

        internal Enumerator(object? value, int count)
        {
            if (value is Span span)
            {
                _values = null;
                _current = span;
                _count = 1;
            }
            else
            {
                _current = null;
                _values = Unsafe.As<Span[]>(value);
                _count = count;
            }

            _index = 0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Enumerator"/> struct.
        /// </summary>
        /// <param name="values">The <see cref="SpanCollection"/> to enumerate.</param>
        public Enumerator(ref SpanCollection values)
            : this(values._values, values.Count)
        {
        }

        /// <summary>
        /// Gets the element at the current position of the enumerator.
        /// </summary>
        public Span Current => _current!;

        object? IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next element of the <see cref="SpanCollection"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the enumerator was successfully advanced to the next element; <see langword="false"/> if the enumerator has passed the end of the <see cref="SpanCollection"/>.</returns>
        public bool MoveNext()
        {
            int index = _index;
            if (index < 0)
            {
                return false;
            }

            var values = _values;
            if (values != null)
            {
                if (index < _count)
                {
                    _index = index + 1;
                    _current = values[index];
                    return true;
                }

                _index = -1;
                return false;
            }

            _index = -1; // sentinel value
            return _current != null;
        }

        void IEnumerator.Reset() => throw new NotSupportedException();

        /// <summary>
        /// Releases all resources used by the <see cref="Enumerator" />.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
