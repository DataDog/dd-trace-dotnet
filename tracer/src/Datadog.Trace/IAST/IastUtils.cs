// <copyright file="IastUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;

namespace Datadog.Trace.Iast
{
    internal static class IastUtils
    {
        private static int GetHashCodeArray(Array objects)
        {
            int hash = 17;

            foreach (var element in objects)
            {
                var hashCode = (element is Array array) ? GetHashCodeArray(array) : element?.GetHashCode();
                unchecked
                {
                    hash = (hash * 23) + (hashCode ?? 0);
                }
            }

            return hash;
        }

        public static int GetHashCode(params object?[] objects)
        {
            return GetHashCodeArray(objects);
        }
    }
}
