// <copyright file="ISpanContextExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

#nullable enable

namespace Datadog.Trace
{
    /// <summary>
    /// The ISpanContextExtractor is responsible for extracting SpanContext in the rare cases where the Tracer couldn't propagate it itself.
    /// This can happen for instance when libraries add an extra layer above the instrumented ones
    /// (eg consuming Kafka messages and enqueuing them prior to generate a span).
    /// </summary>
    public interface ISpanContextExtractor
    {
        /// <summary>
        /// Given a SpanContext carrier and a function to access the values, this method will extract SpanContext if any
        /// </summary>
        /// <param name="carrier">The carrier of the SpanContext. Often a header (http, kafka message header...)</param>
        /// <param name="getter">Given a key name, returns values from the carrier</param>
        /// <typeparam name="TCarrier">Type of the carrier</typeparam>
        /// <returns>A potentially null Datadog SpanContext</returns>
        public ISpanContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter);

        /// <summary>
        /// Given a SpanContext carrier and a function to access the values, this method will extract SpanContext if any.
        /// If the carrier contains PathwayContext information, this is included in the SpanContext
        /// </summary>
        /// <param name="carrier">The carrier of the SpanContext. Often a header (kafka message header...)</param>
        /// <param name="stringGetter">Given a key name, returns values from the carrier as a string</param>
        /// <param name="binaryGetter">Given a key name, returns  the first values from the carrier as a byte[]</param>
        /// <typeparam name="TCarrier">Type of the carrier</typeparam>
        /// <returns>A potentially null Datadog SpanContext</returns>
        public ISpanContext? Extract<TCarrier>(
            TCarrier carrier,
            Func<TCarrier, string, IEnumerable<string?>> stringGetter,
            Func<TCarrier, string, byte[]?> binaryGetter);
    }
}
