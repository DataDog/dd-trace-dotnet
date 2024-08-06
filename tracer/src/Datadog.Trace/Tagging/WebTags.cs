// <copyright file="WebTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal.SourceGenerators;

namespace Datadog.Trace.Internal.Tagging
{
    internal partial class WebTags : InstrumentationTags, IHasStatusCode
    {
        [Tag(Trace.Internal.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Server;

        [Tag(Trace.Internal.Tags.HttpUserAgent)]
        public string HttpUserAgent { get; set; }

        [Tag(Trace.Internal.Tags.HttpMethod)]
        public string HttpMethod { get; set; }

        [Tag(Trace.Internal.Tags.HttpRequestHeadersHost)]
        public string HttpRequestHeadersHost { get; set; }

        [Tag(Trace.Internal.Tags.HttpUrl)]
        public string HttpUrl { get; set; }

        [Tag(Trace.Internal.Tags.HttpStatusCode)]
        public string HttpStatusCode { get; set; }

        [Tag(Trace.Internal.Tags.NetworkClientIp)]
        public string NetworkClientIp { get; set; }

        [Tag(Trace.Internal.Tags.HttpClientIp)]
        public string HttpClientIp { get; set; }
    }
}
