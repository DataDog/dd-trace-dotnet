// <copyright file="EnumerableExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Iast.Helpers;

internal static class EnumerableExtensions
{
    public static IReadOnlyCollection<T>? Materialize<T>(this IEnumerable<T>? values)
    {
        if (values is null)
        {
            return null;
        }

        return values as IReadOnlyCollection<T> ?? values.ToList();
    }
}
