// <copyright file="IastUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Iast;

internal static class IastUtils
{
    private const int StartHash = 17;

    // Avoid infinite loops
    private const int MaxDepth = 5;

    public static int GetHashCodeForArray(Array objects, int actualDepth = 0)
    {
        int hash = 17;

        if (actualDepth >= MaxDepth)
        {
            return objects?.GetHashCode() ?? 0;
        }

        foreach (var element in objects)
        {
            var hashCode = (element is Array array) ? GetHashCodeForArray(array, actualDepth + 1) : element?.GetHashCode();
            unchecked
            {
                hash = (hash * 23) + (hashCode ?? 0);
            }
        }

        return hash;
    }

    public static int GetHashCode<T>(T value)
    {
        unchecked
        {
            return (StartHash * 23) + GetHash(value);
        }
    }

    public static int GetHashCode<T1, T2>(T1 value1, T2 value2)
    {
        var hash = StartHash;
        unchecked
        {
            hash = (hash * 23) + GetHash(value1);
            hash = (hash * 23) + GetHash(value2);
        }

        return hash;
    }

    public static int GetHashCode<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
    {
        var hash = StartHash;
        unchecked
        {
            hash = (hash * 23) + GetHash(value1);
            hash = (hash * 23) + GetHash(value2);
            hash = (hash * 23) + GetHash(value3);
        }

        return hash;
    }

    private static int GetHash<T>(T element)
       => ((element is Array array)
            ? GetHashCodeForArray(array, 1)
            : element?.GetHashCode() ?? 0);

    public static int IdentityHashCode(object item)
    {
        return (item?.GetHashCode() ?? 0);
    }

    public static Range[] GetRangesForString(string stringValue, Source source)
    {
        return new Range[] { new Range(0, stringValue.Length, source) };
    }
}
