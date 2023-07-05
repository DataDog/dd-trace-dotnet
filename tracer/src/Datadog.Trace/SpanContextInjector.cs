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
    /// <inheritdoc />
    public class SpanContextInjector : ISpanContextInjector
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SpanContextExtractor>();

        /// <inheritdoc />
        [PublicApi]
        public void Inject<TCarrier>(TCarrier carrier, Action<TCarrier, string, string> setter)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContextInjector_Inject);

            if (Tracer.Instance.ActiveScope.Span.Context is SpanContext spanContext)
            {
                SpanContextPropagator.Instance.Inject(spanContext, carrier, setter);
            }
        }
    }
}
