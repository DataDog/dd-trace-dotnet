// <copyright file="ISpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace
{
    /// <summary>
    /// Span context interface.
    /// </summary>
    public partial interface ISpanContext
    {
        /// <summary>
        /// Gets the 64-bit trace id, or the lower 64 bits of a 128-bit trace id.
        /// </summary>
        ulong TraceId { get; }

        /// <summary>
        /// Gets the 64-bit span identifier.
        /// </summary>
        ulong SpanId { get; }

        /// <summary>
        /// Gets the service name to propagate to child spans.
        /// </summary>
        string? ServiceName { get; }
    }
}
