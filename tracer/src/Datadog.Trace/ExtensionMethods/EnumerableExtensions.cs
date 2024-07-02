// <copyright file="EnumerableExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.ExtensionMethods;

internal static class EnumerableExtensions
{
#if !NET6_0_OR_GREATER
    // Polyfill for FirstOrDefault overload that takes a default value
    // https://github.com/dotnet/runtime/blob/5535e31a712343a63f5d7d796cd874e563e5ac14/src/libraries/System.Linq/src/System/Linq/First.cs#L60C13-L61C50
    public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate, TSource defaultValue)
    {
        if (source == null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(source));
        }

        if (predicate == null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(predicate));
        }

        foreach (var element in source)
        {
            if (predicate(element))
            {
                return element;
            }
        }

        return defaultValue;
    }
#endif
}
