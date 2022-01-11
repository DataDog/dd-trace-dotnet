// <copyright file="StringEnumerable.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Util;

internal readonly struct StringEnumerable : IEnumerable<string?>
{
    public static readonly StringEnumerable Empty = new(Array.Empty<string>());

    private readonly string? _value;
    private readonly IEnumerable<string?>? _values;

    public StringEnumerable(string? value)
        : this()
    {
        _value = value;
        _values = null;
    }

    public StringEnumerable(IEnumerable<string?>? values)
    {
        _value = null;
        _values = values;
    }

#if !NETFRAMEWORK
    public StringEnumerable(Microsoft.Extensions.Primitives.StringValues values)
        : this()
    {
        if (values.Count == 1)
        {
            // implicit conversion to string
            _value = values;
        }
        else
        {
            // returns the internal StringValues array without allocating a new one
            _values = (string[])values;
        }
    }
#endif

    public IEnumerator<string?> GetEnumerator()
    {
        if (_values is not null)
        {
            return _values.GetEnumerator();
        }

        if (_value is not null)
        {
            return new SingleEnumerator(_value);
        }

        return Enumerable.Empty<string?>().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private sealed class SingleEnumerator : IEnumerator<string>
    {
        private readonly string _current;
        private bool _moved;

        public SingleEnumerator(string current)
        {
            _current = current;
        }

        public string Current => _current;

        object? IEnumerator.Current => _current;

        public bool MoveNext()
        {
            // only allow MoveNext() once
            if (_moved)
            {
                return false;
            }

            _moved = true;
            return true;
        }

        public void Reset()
        {
            _moved = false;
        }

        public void Dispose()
        {
        }
    }
}
