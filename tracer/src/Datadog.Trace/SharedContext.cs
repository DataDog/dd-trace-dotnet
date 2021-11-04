// <copyright file="SharedContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Datadog.Trace
{
    /// <summary>
    /// Class to share context between Tracers from different versions
    /// </summary>
    public static class SharedContext
    {
        private static IDictionary<string, string> _distributedTrace;

        /// <summary>
        /// Gets the internal distributed trace object
        /// </summary>
        /// <returns>Shared distributed trace object instance</returns>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
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
        public static void SetDistributedTrace(object value)
        {
        }

        /// <summary>
        /// Get shared SpanContext instance
        /// </summary>
        /// <returns>Shared span context object instance</returns>
        public static SpanContext GetSpanContext()
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
        public static void SetSpanContext(SpanContext spanContext)
        {
            var values = (IDictionary<string, string>)GetDistributedTrace();

            if (spanContext == null)
            {
                values?.Clear();
                return;
            }

            if (values == null)
            {
                values = new Dictionary<string, string>();
                SetDistributedTrace(values);
            }

            Inject(values, spanContext);
        }

        // Implementations

        private static object GetDistributedTraceImpl()
        {
            return (IReadOnlyDictionary<string, string>)_distributedTrace;
        }

        private static void SetDistributedTraceImpl(object value)
        {
            _distributedTrace = (IDictionary<string, string>)value;
        }

        // Propagation

        private static SpanContext Extract(IReadOnlyDictionary<string, string> values)
        {
            if (values == null)
            {
                return null;
            }

            return SpanContextPropagator.Instance.Extract(
                values,
                (c, key) =>
                {
                    return c.TryGetValue(key, out var value) ?
                               new[] { value } :
                               Enumerable.Empty<string>();
                });
        }

        private static void Inject(IDictionary<string, string> values, SpanContext spanContext)
        {
            SpanContextPropagator.Instance.Inject(
                spanContext,
                values,
                (c, headerKey, headerValue) => c[headerKey] = headerValue);
        }
    }
}
