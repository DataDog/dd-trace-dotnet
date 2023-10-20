// <copyright file="HttpHeadersCodec.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Globalization;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using OpenTracing.Propagation;

namespace Datadog.Trace.OpenTracing
{
    internal class HttpHeadersCodec : ICodec
    {
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "DD0002:Incorrect usage of public API", Justification = "We must access the SpanContextPropagator helpers via public accessors so they can get redirected to the automatic tracer's SpanContextPropagator.Instance when a version-conflict scenario is detected.")]
        private static readonly SpanContextExtractor SpanContextExtractor = new();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "DD0002:Incorrect usage of public API", Justification = "We must access the SpanContextPropagator helpers via public accessors so they can get redirected to the automatic tracer's SpanContextPropagator.Instance when a version-conflict scenario is detected.")]
        private static readonly SpanContextInjector SpanContextInjector = new();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "DD0002:Incorrect usage of public API", Justification = "We must access the SpanContextPropagator helpers via public accessors so they can get redirected to the automatic tracer's SpanContextPropagator.Instance when a version-conflict scenario is detected.")]
        public global::OpenTracing.ISpanContext Extract(object carrier)
        {
            var map = carrier as ITextMap;

            if (map == null)
            {
                throw new ArgumentException("Carrier should have type ITextMap", nameof(carrier));
            }

            IHeadersCollection headers = new TextMapHeadersCollection(map);
            var propagationContext = SpanContextExtractor.Extract(headers, (carrier, key) => carrier.GetValues(key));
            return new OpenTracingSpanContext(propagationContext);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "DD0002:Incorrect usage of public API", Justification = "We must access the SpanContextPropagator helpers via public accessors so they can get redirected to the automatic tracer's SpanContextPropagator.Instance when a version-conflict scenario is detected.")]
        public void Inject(global::OpenTracing.ISpanContext context, object carrier)
        {
            var map = carrier as ITextMap;

            if (map == null)
            {
                throw new ArgumentException("Carrier should have type ITextMap", nameof(carrier));
            }

            IHeadersCollection headers = new TextMapHeadersCollection(map);

            if (context is OpenTracingSpanContext otSpanContext && otSpanContext.Context is SpanContext ddSpanContext)
            {
                // this is a Datadog context
                SpanContextInjector.Inject(headers, (carrier, key, value) => carrier.Set(key, value), ddSpanContext);
            }
            else
            {
                // any other OpenTracing.ISpanContext
                headers.Set(HttpHeaderNames.TraceId, context.TraceId.ToString(InvariantCulture));
                headers.Set(HttpHeaderNames.ParentId, context.SpanId.ToString(InvariantCulture));
            }
        }
    }
}
