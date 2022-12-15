// <copyright file="StringSegment.Enumerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics;

namespace Datadog.Trace.Util;

[DebuggerDisplay("{ToString(),raw}")]
internal readonly ref partial struct StringSegment
{
    public ref struct Enumerator
    {
        private readonly StringSegment _segment;

        private int _index;
        private char _current;

        internal Enumerator(StringSegment segment)
        {
            _segment = segment;

            _index = segment._start;
            _current = default;
        }

        public readonly char Current => _current;

        public bool MoveNext()
        {
            if (_index < _segment.Length)
            {
                _current = _segment[_index];
                _index++;
                return true;
            }

            return MoveNextRare();
        }

        private bool MoveNextRare()
        {
            _index = _segment.Length + 1;
            _current = default;
            return false;
        }
    }
}
