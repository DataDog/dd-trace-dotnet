// <copyright file="SpanContextExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

#nullable enable

namespace Datadog.Trace
{
    /// <summary>
    /// The <see cref="SpanContextExtractor"/> is responsible for extracting <see cref="ISpanContext"/> in the rare cases where the Tracer couldn't propagate it itself.
    /// This can happen for instance when libraries add an extra layer above the instrumented ones
    /// (eg consuming Kafka messages and enqueuing them prior to generate a span).
    /// When messageType and target are specified, also used to set data streams monitoring checkpoints (if enabled).
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
        /// Given a SpanContext carrier and a function to access the values, this method will extract the <see cref="ISpanContext"/> if any
        /// </summary>
        /// <param name="carrier">The carrier of the SpanContext. Often a header (http, kafka message header...)</param>
        /// <param name="getter">Given a key name, returns values from the carrier. Should return an empty collection if the requested key is not present.</param>
        /// <typeparam name="TCarrier">Type of the carrier</typeparam>
        /// <returns>A potentially null Datadog <see cref="ISpanContext"/></returns>
        [Instrumented]
        public ISpanContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter)
            => null;

        /// <summary>
        /// Given a SpanContext carrier and a function to access the values, this method will extract the SpanContext
        /// and the PathwayContext, and will set a DataStreams Monitoring checkpoint if enabled.
        /// You should only call <see cref="ExtractIncludingDsm{TCarrier}"/> once on the message <paramref name="carrier"/>. Calling
        /// multiple times may lead to incorrect stats when using Data Streams Monitoring.
        /// </summary>
        /// <param name="carrier">The carrier of the SpanContext. Often a header (http, kafka message header...)</param>
        /// <param name="getter">Given a key name, returns values from the carrier. Should return an empty collection if the requested key is not present.</param>
        /// <param name="messageType">For Data Streams Monitoring: The type of messaging system where the message is coming from.</param>
        /// <param name="source">For Data Streams Monitoring: The queue or topic where the message is coming from.</param>
        /// <typeparam name="TCarrier">Type of the carrier</typeparam>
        /// <returns>A potentially null Datadog SpanContext</returns>
        [Instrumented]
        public ISpanContext? ExtractIncludingDsm<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, string messageType, string source)
            => null;
    }
}
