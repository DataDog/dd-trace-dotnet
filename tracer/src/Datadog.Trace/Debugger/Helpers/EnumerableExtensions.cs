// <copyright file="EnumerableExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Debugger.Helpers;

internal static class EnumerableExtensions
{
    public static bool NullableSequentialEquals<T>(this IEnumerable<T> @this, IEnumerable<T> other)
    {
        if (ReferenceEquals(null, @this))
        {
            return ReferenceEquals(null, other);
        }

        if (ReferenceEquals(null, other))
        {
            return false;
        }

        return @this.SequenceEqual(other);
    }
}
