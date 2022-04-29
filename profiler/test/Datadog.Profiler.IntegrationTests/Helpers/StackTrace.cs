// <copyright file="StackTrace.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Profiler.IntegrationTests.Helpers;

internal class StackTrace : List<StackFrame>, IComparable<StackTrace>
{
    public StackTrace(params StackFrame[] items)
        : base(items)
    {
    }

    public StackTrace(IEnumerable<StackFrame> items)
        : base(items)
    {
    }

    public int CompareTo(StackTrace other)
    {
        // IComparable is needed for FluentAssertions
        if (other != null && Count == other.Count && this.SequenceEqual(other))
        {
            return 0;
        }

        return 1;
    }

    public override bool Equals(object obj)
    {
        return obj is StackTrace other && Count == other.Count && this.SequenceEqual(other);
    }

    public override int GetHashCode() => Count;

    public override string ToString() => string.Join(Environment.NewLine, this);
}
