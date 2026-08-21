// <copyright file="HashCodeBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Util;

internal struct HashCodeBuilder
{
    private int _hashCode;
    private bool _hasValue;

    public static int Combine<T1, T2>(T1 value1, T2 value2)
    {
        var builder = new HashCodeBuilder();
        builder.Add(value1);
        builder.Add(value2);
        return builder.ToHashCode();
    }

    public static int Combine<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
    {
        var builder = new HashCodeBuilder();
        builder.Add(value1);
        builder.Add(value2);
        builder.Add(value3);
        builder.Add(value4);
        return builder.ToHashCode();
    }

    public void Add<T>(T value)
    {
        unchecked
        {
            var valueHashCode = value is null ? 0 : EqualityComparer<T>.Default.GetHashCode(value);
            _hashCode = ((_hasValue ? _hashCode : 17) * 31) + valueHashCode;
            _hasValue = true;
        }
    }

    public int ToHashCode() => _hashCode;
}
