// <copyright file="HttpHeadersCodec.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Globalization;
using Datadog.Trace.Headers;
using OpenTracing.Propagation;

namespace Datadog.Trace.OpenTracing
{
    internal class HttpHeadersCodec : ICodec
    {
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        public global::OpenTracing.ISpanContext Extract(object carrier)
        {
            var map = carrier as ITextMap;

            if (map == null)
            {
                throw new ArgumentException("Carrier should have type ITextMap", nameof(carrier));
            }

            IHeadersCollection headers = new TextMapHeadersCollection(map);
            var propagationContext = SpanContextPropagator.Instance.Extract(headers);
            return new OpenTracingSpanContext(propagationContext);
        }

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
                SpanContextPropagator.Instance.Inject(ddSpanContext, headers);
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
