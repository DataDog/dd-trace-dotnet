// <copyright file="StringBuilderCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.Util
{
    /// <summary>
    /// Provide a pooled reusable instance of StringBuilder.
    /// </summary>
    internal static class StringBuilderCache
    {
        internal const int MaxBuilderSize = 360;
        internal const int MaxBuilderCount = 128;
        private static object _lockObject = new object();
        private static Queue<StringBuilder> _stringBuilderQueue = new Queue<StringBuilder>(MaxBuilderCount);

        public static StringBuilder Acquire(int capacity)
        {
            if (capacity > MaxBuilderSize)
            {
                return new StringBuilder(capacity); // Avoid the lock where possible.
            }

            StringBuilder stringBuilder = null;
            lock (_lockObject)
            {
                if (_stringBuilderQueue.Count > 0)
                {
                    stringBuilder = _stringBuilderQueue.Dequeue();
                }
            }

            // Rather than creating a new builder to avoid fragmentation
            // set capacity. Since any returned builder can and will cycle
            // out at some point, we don't have to worry about OOM.
            stringBuilder?.Clear();
            stringBuilder?.EnsureCapacity(capacity);
            return stringBuilder ?? new StringBuilder(capacity);
        }

        public static string GetStringAndRelease(StringBuilder stringBuilder)
        {
            string outputString = stringBuilder.ToString();

            if (stringBuilder.Capacity < MaxBuilderSize)
            {
                lock (_lockObject)
                {
                    if (_stringBuilderQueue.Count < MaxBuilderCount)
                    {
                        _stringBuilderQueue.Enqueue(stringBuilder);
                    }
                }
            }

            return outputString;
        }
    }
}
