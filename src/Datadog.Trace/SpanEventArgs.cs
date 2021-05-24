// <copyright file="SpanEventArgs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    /// <summary>
    /// EventArgs for a Span
    /// </summary>
    internal readonly struct SpanEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpanEventArgs"/> struct.
        /// Creates a new <see cref="SpanEventArgs"/> using <paramref name="span"/>
        /// </summary>
        /// <param name="span">The <see cref="Span"/> used to initialize the <see cref="SpanEventArgs"/> object.</param>
        public SpanEventArgs(Span span) => Span = span;

        internal Span Span { get; }
    }
}
