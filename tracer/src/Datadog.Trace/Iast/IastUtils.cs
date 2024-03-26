// <copyright file="IastUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Iast;

internal static class IastUtils
{
    // From: https://probablydance.com/2018/06/16/fibonacci-hashing-the-optimization-that-the-world-forgot-or-a-better-alternative-to-integer-modulo/
    // (Although, not sure how relevant it is in this case)
    private const int GoldenRatio = 1618033987;

    private const int StartHash = 17;

    // Avoid infinite loops
    private const int MaxDepth = 5;

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
    {
        var hash =
            element switch
            {
                string s => s.GetStaticHashCode(),
                byte b => b,
                short s => s,
                int i => i,
                long l => (int)l ^ (int)(l >> 32),
                _ => element?.GetHashCode() ?? 0
            };

        return hash;
    }

    public static int IdentityHashCode(object item)
    {
        return (item?.GetHashCode() ?? 0);
    }

    public static Range[] GetRangesForString(string stringValue, Source source)
    {
        return new Range[] { new Range(0, stringValue.Length, source) };
    }

    internal static unsafe int GetStaticHashCode(this string? target)
    {
        if (target == null)
        {
            return -1;
        }

        unchecked
        {
            fixed (char* charPtr = target)
            {
                var int32Length = target.Length / 2;
                var intPtr = (int*)charPtr;

                var hash = StartHash;
                for (var i = 0; i < int32Length; i++)
                {
                    hash += intPtr[i] * GoldenRatio;
                }

                if (target.Length % 2 != 0)
                {
                    hash += ((int)charPtr[target.Length - 1]) * GoldenRatio;
                }

                return hash;
            }
        }
    }
}
