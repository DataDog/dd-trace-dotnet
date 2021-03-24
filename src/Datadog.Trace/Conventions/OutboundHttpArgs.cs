using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Conventions
{
    /// <summary>
    /// Arguments used by <c>"IOutboundHttpConvention"</c>.
    /// </summary>
    internal readonly struct OutboundHttpArgs
    {
        /// <summary>
        /// Optional span ID.
        /// </summary>
        public readonly ulong? SpanId;

        /// <summary>
        /// Request's HTTP method.
        /// </summary>
        public readonly string HttpMethod;

        /// <summary>
        /// Request's URI.
        /// </summary>
        public readonly Uri RequestUri;

        /// <summary>
        /// Request's URI.
        /// </summary>
        public readonly IntegrationInfo IntegrationInfo;

        public OutboundHttpArgs(ulong? spanId, string httpMethod, Uri requestUri, IntegrationInfo integrationInfo)
        {
            SpanId = spanId;
            HttpMethod = httpMethod;
            RequestUri = requestUri;
            IntegrationInfo = integrationInfo;
        }
    }
}