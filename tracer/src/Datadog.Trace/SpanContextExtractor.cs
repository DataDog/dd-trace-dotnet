// <copyright file="SpanContextExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

#nullable enable

namespace Datadog.Trace
{
    /// <inheritdoc />
    public class SpanContextExtractor : ISpanContextExtractor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SpanContextExtractor>();

        /// <inheritdoc />
        public ISpanContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter)
        {
            var spanContext = SpanContextPropagator.Instance.Extract(carrier, getter);
            if (spanContext is not null
             && Tracer.Instance.TracerManager.DataStreamsManager is { IsEnabled: true } dsm
             && getter(carrier, DataStreamsPropagationHeaders.TemporaryEdgeTags).FirstOrDefault() is { Length: > 0 } edgeTagString)
            {
                var base64PathwayContext = getter(carrier, DataStreamsPropagationHeaders.TemporaryBase64PathwayContext).FirstOrDefault();
                var pathwayContext = TryGetPathwayContext(base64PathwayContext);

                var edgeTags = edgeTagString.Split(',');
                spanContext.MergePathwayContext(pathwayContext);
                spanContext.SetCheckpoint(dsm, CheckpointKind.Consume, edgeTags);
            }

            return spanContext;
        }

        private static PathwayContext? TryGetPathwayContext(string? base64PathwayContext)
        {
            if (string.IsNullOrEmpty(base64PathwayContext))
            {
                return null;
            }

            try
            {
                var bytes = Convert.FromBase64String(base64PathwayContext);
                return PathwayContextEncoder.Decode(bytes);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error extracting pathway context from base64 encoded pathway {Base64PathwayContext}", base64PathwayContext);
            }

            return null;
        }
    }
}
