// <copyright file="SpanContextInjector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

#nullable enable

namespace Datadog.Trace
{
    /// <summary>
    /// The SpanContextInjector is responsible for injecting SpanContext in the rare cases where the Tracer couldn't propagate it itself.
    /// This can happen for instance when we don't support a specific library
    /// </summary>
    public class SpanContextInjector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContextInjector"/> class
        /// </summary>
        [PublicApi]
        public SpanContextInjector()
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContextInjector_Ctor);
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
        [PublicApi]
        public void Inject<TCarrier>(TCarrier carrier, Action<TCarrier, string, string> setter, ISpanContext context)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContextInjector_Inject);

            if (context is SpanContext spanContext)
            {
                SpanContextPropagator.Instance.Inject(spanContext, carrier, setter);
            }
        }
    }
}
