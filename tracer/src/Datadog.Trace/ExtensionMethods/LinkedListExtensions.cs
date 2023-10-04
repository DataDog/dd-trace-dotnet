// <copyright file="LinkedListExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;

namespace Datadog.Trace.Iast.Helpers;

internal static class LinkedListExtensions
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
}
