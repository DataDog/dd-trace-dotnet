// <copyright file="CollectionExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Datadog.Trace.Iast.Helpers;

internal static class CollectionExtensions
{
    public static T? Poll<T>(this LinkedList<T>? list)
        where T : struct
   {
        if (list != null && list.First != null)
        {
            var res = list.First.Value;
            list.RemoveFirst();
            return res;
        }

        return null;
    }

    public static T? Peek<T>(this LinkedList<T>? list)
        where T : struct
    {
        if (list != null && list.First != null)
        {
            var res = list.First.Value;
            return res;
        }

        return null;
    }

    public static bool IsEmpty<T>(this LinkedList<T>? list)
    {
        return list == null || list.Count == 0;
    }

#pragma warning disable 8714
    public static TV? GetAndRemove<TK, TV>(this Dictionary<TK, TV>? map, TK key)
        where TV : class
    {
        if (map != null && map.TryGetValue(key, out var val))
        {
            map.Remove(key);
            return val;
        }

        return null;
    }

    public static TV Get<TK, TV>(this Dictionary<TK, TV> map, TK key, Func<TK, TV> computeIfAbsent)
        where TV : class
    {
        if (map.TryGetValue(key, out var val))
        {
            return val;
        }

        var newVal = computeIfAbsent(key);
        map[key] = newVal;
        return newVal;
    }
#pragma warning restore 8714
}
