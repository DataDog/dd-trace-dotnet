// <copyright file="ReadOnlySpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    internal class ReadOnlySpanContext : ISpanContext
    {
        public ReadOnlySpanContext(TraceId traceId, ulong spanId, string serviceName)
        {
            TraceId128 = traceId;
            SpanId = spanId;
            ServiceName = serviceName;
        }

        /// <summary>
        /// Gets the lower 64 bits of the 128-bit trace identifier.
        /// </summary>
        ulong ISpanContext.TraceId => TraceId128.Lower;

        /// <summary>
        /// Gets the 128-bit trace identifier.
        /// </summary>
        public TraceId TraceId128 { get; }

        /// <summary>
        /// Gets the 64-bit span identifier.
        /// </summary>
        public ulong SpanId { get; }

        /// <summary>
        /// Gets the service name to propagate to child spans.
        /// </summary>
        public string ServiceName { get; }
    }
}
