// <copyright file="SpanContextExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

#nullable enable

namespace Datadog.Trace
{
    /// <summary>
    /// The <see cref="SpanContextExtractor"/> is responsible for extracting <see cref="ISpanContext"/> in the rare cases
    /// where the Tracer couldn't propagate it itself. This can happen for instance when libraries add an extra
    /// layer above the instrumented ones (eg consuming Kafka messages and enqueuing them prior to generate a span).
    /// When enabled (and present in the headers) also used to set data streams monitoring checkpoints.
    /// </summary>
    public sealed class SpanContextExtractor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContextExtractor"/> class
        /// </summary>
        [Instrumented]
        public SpanContextExtractor()
        {
        }

        /// <summary>
        /// Given a SpanContext carrier and a function to access the values, this method will extract the <see cref="ISpanContext"/> if any.
        /// </summary>
        /// <param name="carrier">The carrier of the SpanContext. Often a header (http, kafka message header...)</param>
        /// <param name="getter">Given a key name, returns values from the carrier</param>
        /// <typeparam name="TCarrier">Type of the carrier</typeparam>
        /// <returns>A potentially null Datadog <see cref="ISpanContext"/></returns>
        [Instrumented]
        public ISpanContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter)
            => null;
    }
}
