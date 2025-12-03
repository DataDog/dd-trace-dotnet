// <copyright file="SpanContextComparer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared
{
    /// <summary>
    /// Comparer for SpanContext deduplication in batch operations
    /// </summary>
    internal class SpanContextComparer : IEqualityComparer<SpanContext>
    {
        public bool Equals(SpanContext? x, SpanContext? y)
        {
            if (x == null || y == null)
            {
                return x == y;
            }

            return x.TraceId128 == y.TraceId128 && x.SpanId == y.SpanId;
        }

        public int GetHashCode(SpanContext obj)
        {
            return HashCode.Combine(obj.TraceId128, obj.SpanId);
        }
    }
}
