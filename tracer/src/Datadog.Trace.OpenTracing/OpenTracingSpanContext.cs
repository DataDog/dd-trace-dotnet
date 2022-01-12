// <copyright file="OpenTracingSpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Datadog.Trace.OpenTracing
{
    /// <inheritdoc />
    public class OpenTracingSpanContext : global::OpenTracing.ISpanContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTracingSpanContext"/> class.
        /// </summary>
        /// <param name="traceId">The globally-unique trace id.</param>
        /// <param name="spanId">The span id.</param>
        public OpenTracingSpanContext(ulong traceId, ulong spanId)
            : this(new SpanContext(traceId, spanId, samplingPriority: null, serviceName: null))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTracingSpanContext"/> class.
        /// </summary>
        /// <param name="context">The <see cref="ISpanContext"/> wrapped by this instance.</param>
        internal OpenTracingSpanContext(ISpanContext context)
        {
            Context = context;
        }

        /// <inheritdoc />
        public string TraceId => Context.TraceId.ToString(CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public string SpanId => Context.SpanId.ToString(CultureInfo.InvariantCulture);

        internal ISpanContext Context { get; }

        /// <inheritdoc/>
        IEnumerable<KeyValuePair<string, string>> global::OpenTracing.ISpanContext.GetBaggageItems()
        {
            return Enumerable.Empty<KeyValuePair<string, string>>();
        }
    }
}
