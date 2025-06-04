// <copyright file="HttpHeadersCodec.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Globalization;
using OpenTracing.Propagation;

namespace Datadog.Trace.OpenTracing
{
    internal class HttpHeadersCodec : ICodec
    {
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
        private static readonly SpanContextExtractor Extractor = new();
        private static readonly SpanContextInjector Injector = new();

        public global::OpenTracing.ISpanContext Extract(object carrier)
        {
            var map = carrier as ITextMap;

            if (map == null)
            {
                throw new ArgumentException("Carrier should have type ITextMap", nameof(carrier));
            }

            var propagationContext = Extractor.Extract(map, GetValues);
            return new OpenTracingSpanContext(propagationContext);

            static IEnumerable<string> GetValues(ITextMap textMap, string name)
            {
                foreach (var pair in textMap)
                {
                    if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return pair.Value;
                    }
                }
            }
        }

        public void Inject(global::OpenTracing.ISpanContext context, object carrier)
        {
            var map = carrier as ITextMap;

            if (map == null)
            {
                throw new ArgumentException("Carrier should have type ITextMap", nameof(carrier));
            }

            if (context is OpenTracingSpanContext otSpanContext && otSpanContext.Context is SpanContext ddSpanContext)
            {
                // this is a Datadog context
                Injector.Inject(map, (carrier, name, value) => carrier.Set(name, value), ddSpanContext);
            }
            else
            {
                // any other OpenTracing.ISpanContext
                map.Set(HttpHeaderNames.TraceId, context.TraceId.ToString(InvariantCulture));
                map.Set(HttpHeaderNames.ParentId, context.SpanId.ToString(InvariantCulture));
            }
        }
    }
}
