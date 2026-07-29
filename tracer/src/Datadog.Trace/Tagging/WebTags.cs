// <copyright file="WebTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class WebTags : InstrumentationTags, IHasStatusCode
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Server;

        [Tag(Trace.Tags.HttpUserAgent, OtelName = Trace.Tags.UserAgentOriginal)]
        public string HttpUserAgent { get; set; }

        [Tag(Trace.Tags.HttpMethod, OtelName = Trace.Tags.HttpRequestMethod)]
        public string HttpMethod { get; set; }

        // OpenTelemetry-only: set only when the request method was normalized to "_OTHER"
        // or to its canonical casing, i.e. when it differs from HttpMethod.
        [Tag(Trace.Tags.HttpRequestMethodOriginal)]
        public string HttpRequestMethodOriginal { get; set; }

        // Datadog-only: OpenTelemetry splits this concept into ServerAddress + ServerPort.
        [Tag(Trace.Tags.HttpRequestHeadersHost)]
        public string HttpRequestHeadersHost { get; set; }

        // Datadog-only: OpenTelemetry splits this concept into UrlScheme + UrlPath + UrlQuery.
        [Tag(Trace.Tags.HttpUrl)]
        public string HttpUrl { get; set; }

        // OpenTelemetry-only
        [Tag(Trace.Tags.UrlScheme)]
        public string UrlScheme { get; set; }

        // OpenTelemetry-only
        [Tag(Trace.Tags.UrlPath)]
        public string UrlPath { get; set; }

        // OpenTelemetry-only
        [Tag(Trace.Tags.UrlQuery)]
        public string UrlQuery { get; set; }

        // OpenTelemetry-only
        [Tag(Trace.Tags.ServerAddress)]
        public string ServerAddress { get; set; }

        // OpenTelemetry-only
        [Tag(Trace.Tags.ServerPort)]
        public int? ServerPort { get; set; }

        [Tag(Trace.Tags.HttpStatusCode, OtelName = Trace.Tags.HttpResponseStatusCode)]
        public int? HttpStatusCode { get; set; }

        [Tag(Trace.Tags.NetworkClientIp, OtelName = Trace.Tags.NetworkPeerAddress)]
        public string NetworkClientIp { get; set; }

        [Tag(Trace.Tags.HttpClientIp, OtelName = Trace.Tags.ClientAddress)]
        public string HttpClientIp { get; set; }
    }
}
