using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Conventions
{
    internal class OtelOutboundHttpConvention : IOutboundHttpConvention
    {
        private readonly Tracer _tracer;

        public OtelOutboundHttpConvention(Tracer tracer)
        {
            _tracer = tracer;
        }

        public Scope CreateScope(OutboundHttpArgs args, out HttpTags tags)
        {
            var otelTags = new OtelHttpTags();
            tags = otelTags;

            string operationName = "HTTP " + args.HttpMethod;
            string serviceName = _tracer.Settings.GetServiceName(_tracer, "http-client");
            var scope = _tracer.StartActiveWithTags(operationName, tags: tags, serviceName: serviceName, spanId: args.SpanId);
            scope.Span.Type = SpanTypes.Http;

            var uri = args.RequestUri;
            otelTags.HttpMethod = args.HttpMethod;
            otelTags.HttpUrl = string.Concat(uri.Scheme, Uri.SchemeDelimiter, uri.Authority, uri.PathAndQuery, uri.Fragment);
            otelTags.InstrumentationName = IntegrationRegistry.GetName(args.IntegrationInfo);
            return scope;
        }
    }
}