// <copyright file="SharedContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Datadog.Trace
{
    /// <summary>
    /// Class to share context between Tracers from different versions
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class SharedContext
    {
        private static readonly AsyncLocal<IReadOnlyDictionary<string, string>> DistributedTrace = new();

        /// <summary>
        /// Gets the internal distributed trace object
        /// </summary>
        /// <returns>Shared distributed trace object instance</returns>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static object GetDistributedTrace()
        {
            return null;
        }

        /// <summary>
        /// Sets the internal distributed trace object
        /// </summary>
        /// <param name="value">Shared distributed trace object instance</param>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetDistributedTrace(object value)
        {
        }

        /// <summary>
        /// Get shared SpanContext instance
        /// </summary>
        /// <returns>Shared span context object instance</returns>
        internal static SpanContext GetSpanContext()
        {
            var values = (IReadOnlyDictionary<string, string>)GetDistributedTrace();

            if (values == null || values.Count == 0)
            {
                return null;
            }

            return Extract(values);
        }

        /// <summary>
        /// Sets the shared SpanContext instance
        /// </summary>
        /// <param name="spanContext">Shared span context object instance</param>
        internal static void SetSpanContext(SpanContext spanContext)
        {
            SetDistributedTrace(spanContext);
        }

        private static SpanContext Extract(IReadOnlyDictionary<string, string> values)
        {
            return SpanContextPropagator.Instance.Extract(values);
        }

#pragma warning disable IDE0051 // ReSharper disable UnusedMember.Local - Usage injected at runtime by the profiler

        private static object GetDistributedTraceImpl()
        {
            return DistributedTrace.Value;
        }

        private static void SetDistributedTraceImpl(object value)
        {
            DistributedTrace.Value = (IReadOnlyDictionary<string, string>)value;
        }

#pragma warning restore IDE0051 // ReSharper restore UnusedMember.Local
    }
}
