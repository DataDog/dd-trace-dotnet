// <copyright file="IastUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Iast;

internal static class IastUtils
{
    public static int GetHashCode(Array objects)
    {
        int hash = 17;

        foreach (var element in objects)
        {
            var hashCode = (element is Array array) ? GetHashCode(array) : element?.GetHashCode();
            unchecked
            {
                hash = (hash * 23) + (hashCode ?? 0);
            }
        }

        return hash;
    }

    public static int IdentityHashCode(object item)
    {
        return (item?.GetHashCode() ?? 0);
    }

    public static Range[] GetRangesForString(string obj, Source? source)
    {
        return new Range[] { new Range(0, obj.Length, source) };
    }
}
