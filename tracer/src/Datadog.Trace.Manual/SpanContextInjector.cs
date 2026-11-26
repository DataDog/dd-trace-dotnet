// <copyright file="SpanContextInjector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using Datadog.Trace.SourceGenerators;

#nullable enable

namespace Datadog.Trace
{
    /// <summary>
    /// The SpanContextInjector is responsible for injecting SpanContext in the rare cases where the Tracer couldn't propagate it itself.
    /// This can happen for instance when we don't support a specific library
    /// </summary>
    public sealed class SpanContextInjector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContextInjector"/> class
        /// </summary>
        [Instrumented]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public SpanContextInjector()
        {
        }

        /// <summary>
        /// Given a SpanContext carrier and a function to set a value, this method will inject a SpanContext.
        /// You should only call <see cref="Inject{TCarrier}"/> once on the message <paramref name="carrier"/>. Calling
        /// multiple times may lead to incorrect behaviors.
        /// </summary>
        /// <param name="carrier">The carrier of the SpanContext. Often a header (http, kafka message header...)</param>
        /// <param name="setter">Given a key name and value, sets the value in the carrier</param>
        /// <param name="context">The context you want to inject</param>
        /// <typeparam name="TCarrier">Type of the carrier</typeparam>
        [Instrumented]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Inject<TCarrier>(TCarrier carrier, Action<TCarrier, string, string> setter, ISpanContext context)
        {
        }

        /// <summary>
        /// Given a SpanContext carrier and a function to set a value, this method will inject a SpanContext.
        /// You should only call <see cref="Inject{TCarrier}"/> once on the message <paramref name="carrier"/>. Calling
        /// multiple times may lead to incorrect behaviors.
        /// This method also sets a data streams monitoring checkpoint (if enabled).
        /// </summary>
        /// <param name="carrier">The carrier of the SpanContext. Often a header (http, kafka message header...)</param>
        /// <param name="setter">Given a key name and value, sets the value in the carrier</param>
        /// <param name="context">The context you want to inject</param>
        /// <param name="messageType">For Data Streams Monitoring: The type of messaging system where the data being injected will be sent.</param>
        /// <param name="target">For Data Streams Monitoring: The queue or topic where the data being injected will be sent.</param>
        /// <typeparam name="TCarrier">Type of the carrier</typeparam>
        [Instrumented]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void InjectIncludingDsm<TCarrier>(TCarrier carrier, Action<TCarrier, string, string> setter, ISpanContext context, string messageType, string target)
        {
        }

        /// <summary>
        /// Given a SpanContext carrier and a function to set a value, this method will inject a SpanContext
        /// and set a data streams monitoring checkpoint (if enabled), using <paramref name="options"/>.
        /// Use it to record where data starts, for example a change written to a database, before it reaches a queue.
        /// Unless <see cref="DataStreamsCheckpointOptions.ChainFromPrevious"/> is set, the checkpoint starts a new pathway.
        /// </summary>
        /// <param name="carrier">The carrier of the SpanContext. Often a header (http, kafka message header...)</param>
        /// <param name="setter">Given a key name and value, sets the value in the carrier</param>
        /// <param name="context">The context you want to inject</param>
        /// <param name="messageType">For Data Streams Monitoring: The type of system the data is written to (for example "sqlserver").</param>
        /// <param name="target">For Data Streams Monitoring: The queue, topic or table the data is written to.</param>
        /// <param name="options">When the checkpoint happened, and whether to chain to the previous checkpoint.</param>
        /// <typeparam name="TCarrier">Type of the carrier</typeparam>
        public void InjectIncludingDsm<TCarrier>(TCarrier carrier, Action<TCarrier, string, string> setter, ISpanContext context, string messageType, string target, DataStreamsCheckpointOptions options)
            => InjectIncludingDsmWithOptions(carrier, setter, context, messageType, target, ToUnixMilliseconds(options.Timestamp), ToUnixMilliseconds(options.SourceTimestamp), options.ChainFromPrevious);

        // 0 means "not set": the instrumented method takes primitives so it can be intercepted across versions.
        private static long ToUnixMilliseconds(DateTimeOffset? time) => time?.ToUnixTimeMilliseconds() ?? 0;

        [Instrumented]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void InjectIncludingDsmWithOptions<TCarrier>(TCarrier carrier, Action<TCarrier, string, string> setter, ISpanContext context, string messageType, string target, long timestampMs, long sourceTimestampMs, bool chainFromPrevious)
        {
        }
    }
}
