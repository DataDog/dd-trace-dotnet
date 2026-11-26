// <copyright file="SpanContextExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
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
        [MethodImpl(MethodImplOptions.NoInlining)]
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
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ISpanContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter)
            => null;

        /// <summary>
        /// Given a SpanContext carrier and a function to access the values, this method will extract the SpanContext
        /// and the PathwayContext, and will set a DataStreams Monitoring checkpoint if enabled.
        /// You should only call <see cref="ExtractIncludingDsm{TCarrier}(TCarrier, Func{TCarrier, string, IEnumerable{string}}, string, string)"/> once on the message <paramref name="carrier"/>. Calling
        /// multiple times may lead to incorrect stats when using Data Streams Monitoring.
        /// </summary>
        /// <param name="carrier">The carrier of the SpanContext. Often a header (http, kafka message header...)</param>
        /// <param name="getter">Given a key name, returns values from the carrier. Should return an empty collection if the requested key is not present.</param>
        /// <param name="messageType">For Data Streams Monitoring: The type of messaging system where the message is coming from.</param>
        /// <param name="source">For Data Streams Monitoring: The queue or topic where the message is coming from.</param>
        /// <typeparam name="TCarrier">Type of the carrier</typeparam>
        /// <returns>A potentially null Datadog SpanContext</returns>
        [Instrumented]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ISpanContext? ExtractIncludingDsm<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, string messageType, string source)
            => null;

        /// <summary>
        /// Given a SpanContext carrier and a function to access the values, this method will extract the SpanContext
        /// and the PathwayContext, and will set a DataStreams Monitoring checkpoint if enabled, using <paramref name="options"/>.
        /// Unlike <see cref="ExtractIncludingDsm{TCarrier}(TCarrier, Func{TCarrier, string, IEnumerable{string}}, string, string)"/>,
        /// the checkpoint is set even when the carrier has no trace headers, for example for a row read from a database.
        /// </summary>
        /// <param name="carrier">The carrier of the SpanContext. Often a header (http, kafka message header...), or an empty collection.</param>
        /// <param name="getter">Given a key name, returns values from the carrier. Should return an empty collection if the requested key is not present.</param>
        /// <param name="messageType">For Data Streams Monitoring: The type of system the data comes from (for example "sqlserver").</param>
        /// <param name="source">For Data Streams Monitoring: The queue, topic or table the data comes from.</param>
        /// <param name="options">When the checkpoint happened, when the data entered the source, and whether to chain to the previous checkpoint.</param>
        /// <typeparam name="TCarrier">Type of the carrier</typeparam>
        /// <returns>A potentially null Datadog SpanContext</returns>
        public ISpanContext? ExtractIncludingDsm<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, string messageType, string source, DataStreamsCheckpointOptions options)
            => ExtractIncludingDsmWithOptions(carrier, getter, messageType, source, ToUnixMilliseconds(options.Timestamp), ToUnixMilliseconds(options.SourceTimestamp), options.ChainFromPrevious);

        // 0 means "not set": the instrumented method takes primitives so it can be intercepted across versions.
        private static long ToUnixMilliseconds(DateTimeOffset? time) => time?.ToUnixTimeMilliseconds() ?? 0;

        [Instrumented]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private ISpanContext? ExtractIncludingDsmWithOptions<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, string messageType, string source, long timestampMs, long sourceTimestampMs, bool chainFromPrevious)
            => null;
    }
}
