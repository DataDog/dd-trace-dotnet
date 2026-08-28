// <copyright file="WebTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class WebTags : InstrumentationTags, IHasStatusCode, IHasHttpMethod
    {
        // Only allocated when OpenTelemetry semantics are enabled, so that the (currently more common)
        // Datadog-semantics case doesn't pay for fields it never populates.
        private readonly OtelTags _otelTags;

        public WebTags()
            : this(otelSemanticsEnabled: false)
        {
        }

        public WebTags(bool otelSemanticsEnabled)
        {
            _otelTags = otelSemanticsEnabled ? new OtelTags() : null;
        }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Server;

        [Tag(Trace.Tags.HttpUserAgent, OtelName = Trace.Tags.UserAgentOriginal)]
        public string HttpUserAgent { get; set; }

        [Tag(Trace.Tags.HttpMethod, OtelName = Trace.Tags.HttpRequestMethod)]
        public string HttpMethod { get; set; }

        // OpenTelemetry-only: set only when the request method was normalized to "_OTHER"
        // or to its canonical casing, i.e. when it differs from HttpMethod.
        [Tag(Trace.Tags.HttpRequestMethodOriginal)]
        public string HttpRequestMethodOriginal
        {
            get => _otelTags?.HttpRequestMethodOriginal;
            set => _otelTags?.HttpRequestMethodOriginal = value;
        }

        // Datadog-only: OpenTelemetry splits this concept into ServerAddress + ServerPort.
        [Tag(Trace.Tags.HttpRequestHeadersHost)]
        public string HttpRequestHeadersHost { get; set; }

        // Datadog-only: OpenTelemetry splits this concept into UrlScheme + UrlPath + UrlQuery.
        [Tag(Trace.Tags.HttpUrl)]
        public string HttpUrl { get; set; }

        // OpenTelemetry-only
        [Tag(Trace.Tags.UrlScheme)]
        public string UrlScheme
        {
            get => _otelTags?.UrlScheme;
            set => _otelTags?.UrlScheme = value;
        }

        // OpenTelemetry-only
        [Tag(Trace.Tags.UrlPath)]
        public string UrlPath
        {
            get => _otelTags?.UrlPath;
            set => _otelTags?.UrlPath = value;
        }

        // OpenTelemetry-only
        [Tag(Trace.Tags.UrlQuery)]
        public string UrlQuery
        {
            get => _otelTags?.UrlQuery;
            set => _otelTags?.UrlQuery = value;
        }

        // OpenTelemetry-only
        [Tag(Trace.Tags.ServerAddress)]
        public string ServerAddress
        {
            get => _otelTags?.ServerAddress;
            set => _otelTags?.ServerAddress = value;
        }

        // OpenTelemetry-only
        [Tag(Trace.Tags.ServerPort)]
        public int? ServerPort
        {
            get => _otelTags?.ServerPort;
            set => _otelTags?.ServerPort = value;
        }

        [Tag(Trace.Tags.HttpStatusCode, OtelName = Trace.Tags.HttpResponseStatusCode)]
        public int? HttpStatusCode { get; set; }

        [Tag(Trace.Tags.NetworkClientIp, OtelName = Trace.Tags.NetworkPeerAddress)]
        public string NetworkClientIp { get; set; }

        [Tag(Trace.Tags.HttpClientIp, OtelName = Trace.Tags.ClientAddress)]
        public string HttpClientIp { get; set; }

        // Holds the OpenTelemetry-only tag values so that the common Datadog-semantics case
        // doesn't allocate storage for fields it never uses.
        private sealed class OtelTags
        {
            public string HttpRequestMethodOriginal { get; set; }

            public string UrlScheme { get; set; }

            public string UrlPath { get; set; }

            public string UrlQuery { get; set; }

            public string ServerAddress { get; set; }

            public int? ServerPort { get; set; }
        }
    }
}
