// <copyright file="ISpanContextInjector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

#nullable enable

namespace Datadog.Trace
{
    /// <summary>
    /// The ISpanContextInjector is responsible for injecting SpanContext in the rare cases where the Tracer couldn't propagate it itself.
    /// This can happen for instance when we don't support a specific library
    /// </summary>
    public interface ISpanContextInjector
    {
        /// <summary>
        /// Given a SpanContext carrier and a function to access the values, this method will extract SpanContext if any
        /// When enabled (and present in the headers) a data streams monitoring checkpoint is set.
        /// You should only call <see cref="Inject{TCarrier}"/> once on the message <paramref name="carrier"/>. Calling
        /// multiple times may lead to incorrect stats when using Data Streams Monitoring.
        /// </summary>
        /// <param name="carrier">The carrier of the SpanContext. Often a header (http, kafka message header...)</param>
        /// <param name="setter">Given a key name, returns values from the carrier</param>
        /// <typeparam name="TCarrier">Type of the carrier</typeparam>
        public void Inject<TCarrier>(TCarrier carrier, Action<TCarrier, string, string> setter);
    }
}
