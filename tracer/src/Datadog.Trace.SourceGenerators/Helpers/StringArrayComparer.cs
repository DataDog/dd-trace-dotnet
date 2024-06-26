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
            var xArray = x.AsArray();
            var yArray = y.AsArray();
            if (xArray is null && yArray is null)
            {
                return 0;
            }
            else if (xArray is null)
            {
                return -1;
            }
            else if (yArray is null)
            {
                return 1;
            }

            for (int i = 0; i < xArray.Length && i < yArray.Length; i++)
            {
                int comparison = string.Compare(xArray[i], yArray[i], StringComparison.Ordinal);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return xArray.Length.CompareTo(yArray.Length);
        }
    }
}
