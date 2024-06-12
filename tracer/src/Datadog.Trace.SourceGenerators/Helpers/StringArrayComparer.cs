// <copyright file="StringArrayComparer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Datadog.Trace.SourceGenerators.Helpers;

namespace Helpers
{
    internal class StringArrayComparer : IComparer<EquatableArray<string>>
    {
        public static readonly StringArrayComparer Comparer = new StringArrayComparer();

        /// <summary>
        /// Compare this instance to another <see cref="EquatableArray{T}"/>.
        /// </summary>
        /// <param name="x"> First instance </param>
        /// <param name="y"> Second instance </param>
        /// <returns> -1, 0 or 1 </returns>
        public int Compare(EquatableArray<string> x, EquatableArray<string> y)
        {
            if (x._array is null && y._array is null)
            {
                return 0;
            }
            else if (x._array is null)
            {
                return -1;
            }
            else if (y._array is null)
            {
                return 1;
            }

            for (int i = 0; i < x._array.Length && i < y._array.Length; i++)
            {
                int comparison = string.Compare(x._array[i], y._array[i], StringComparison.Ordinal);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return x._array.Length.CompareTo(y._array.Length);
        }
    }
}
