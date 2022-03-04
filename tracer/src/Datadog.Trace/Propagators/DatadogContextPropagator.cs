// <copyright file="DatadogContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Datadog.Trace.Propagators
{
    internal class DatadogContextPropagator : IContextInjector, IContextExtractor
    {
        public void Inject<TCarrier>(SpanContext context, TCarrier carrier, Action<TCarrier, string, string> setter)
        {
            var invariantCulture = CultureInfo.InvariantCulture;

            setter(carrier, HttpHeaderNames.TraceId, context.TraceId.ToString(invariantCulture));
            setter(carrier, HttpHeaderNames.ParentId, context.SpanId.ToString(invariantCulture));

            if (context.Origin != null)
            {
                setter(carrier, HttpHeaderNames.Origin, context.Origin);
            }

            var samplingPriority = context.TraceContext?.SamplingPriority ?? context.SamplingPriority;

            if (samplingPriority != null)
            {
                setter(carrier, HttpHeaderNames.SamplingPriority, samplingPriority.Value.ToString(invariantCulture));
            }

            var datadogTags = context.TraceContext?.Tags?.ToPropagationHeader() ?? context.DatadogTags;

            if (datadogTags != null)
            {
                setter(carrier, HttpHeaderNames.DatadogTags, datadogTags);
            }
        }

        public bool TryExtract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, out SpanContext? spanContext)
        {
            spanContext = null;

            var traceId = ParseUtility.ParseUInt64(carrier, getter, HttpHeaderNames.TraceId);
            if (traceId is null or 0)
            {
                // a valid traceId is required to use distributed tracing
                return false;
            }

            var parentId = ParseUtility.ParseUInt64(carrier, getter, HttpHeaderNames.ParentId) ?? 0;
            var samplingPriority = ParseUtility.ParseInt32(carrier, getter, HttpHeaderNames.SamplingPriority);
            var origin = ParseUtility.ParseString(carrier, getter, HttpHeaderNames.Origin);
            var datadogTags = ParseUtility.ParseString(carrier, getter, HttpHeaderNames.DatadogTags);

            spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, origin)
            {
                DatadogTags = datadogTags,
            };

            return true;
        }
    }
}
