// <copyright file="WebTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class WebTags : InstrumentationTags, IHasStatusCode
    {
        [Tag(Internal.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Server;

        [Tag(Internal.Tags.HttpUserAgent)]
        public string HttpUserAgent { get; set; }

        [Tag(Internal.Tags.HttpMethod)]
        public string HttpMethod { get; set; }

        [Tag(Internal.Tags.HttpRequestHeadersHost)]
        public string HttpRequestHeadersHost { get; set; }

        [Tag(Internal.Tags.HttpUrl)]
        public string HttpUrl { get; set; }

        [Tag(Internal.Tags.HttpStatusCode)]
        public string HttpStatusCode { get; set; }

        [Tag(Internal.Tags.NetworkClientIp)]
        public string NetworkClientIp { get; set; }

        [Tag(Internal.Tags.HttpClientIp)]
        public string HttpClientIp { get; set; }
    }
}
