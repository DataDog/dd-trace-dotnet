// <copyright file="StackTrace.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal class StackTrace : IComparable<StackTrace>
    {
        private readonly IReadOnlyList<StackFrame> _stackFrames;

        public StackTrace(params StackFrame[] items)
            : this((IEnumerable<StackFrame>)items)
        {
        }

        public StackTrace(IEnumerable<StackFrame> items)
        {
            _stackFrames = items.ToList();
        }

        public int FramesCount => _stackFrames.Count;

        public StackFrame this[int offset] => _stackFrames[offset];

        public bool EndWith(StackTrace other)
        {
            if (_stackFrames.Count < other._stackFrames.Count)
            {
                return false;
            }

            for (int i = 0; i < other._stackFrames.Count; i++)
            {
                _stackFrames[i].ToString().Should().Be(other._stackFrames[i].ToString());
            }

            return true;
        }

        public int CompareTo(StackTrace other)
        {
            // IComparable is needed for FluentAssertions
            if (other != null && _stackFrames.Count == other._stackFrames.Count && _stackFrames.SequenceEqual(other._stackFrames))
            {
                return 0;
            }

            return 1;
        }

        public override bool Equals(object obj)
        {
            return obj is StackTrace other && _stackFrames.Count == other._stackFrames.Count && _stackFrames.SequenceEqual(other._stackFrames);
        }

        public override int GetHashCode() => _stackFrames.Count;

        public override string ToString() => string.Join(Environment.NewLine, _stackFrames);

        public bool Contains(StackTrace other)
        {
            if (_stackFrames.Count < other._stackFrames.Count)
            {
                return false;
            }

            int i = 0;
            while (i < _stackFrames.Count && !_stackFrames[i].Equals(other._stackFrames[0]))
            {
                i++;
            }

            if (_stackFrames.Count - i + 1 < other._stackFrames.Count)
            {
                return false;
            }

            int j = 0;

            for (; j < other._stackFrames.Count; i++, j++)
            {
                if (!_stackFrames[i].Equals(other._stackFrames[j]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
