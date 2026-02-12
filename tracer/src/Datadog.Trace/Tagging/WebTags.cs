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

        [Tag(Trace.Tags.HttpUserAgent)]
        public string HttpUserAgent { get; set; }

        [Tag(Trace.Tags.HttpMethod)]
        public string HttpMethod { get; set; }

        [Tag(Trace.Tags.HttpRequestHeadersHost)]
        public string HttpRequestHeadersHost { get; set; }

        [Tag(Trace.Tags.HttpUrl)]
        public string HttpUrl { get; set; }

        [Tag(Trace.Tags.HttpStatusCode)]
        public string HttpStatusCode { get; set; }

        [Tag(Trace.Tags.NetworkClientIp)]
        public string NetworkClientIp { get; set; }

        [Tag(Trace.Tags.HttpClientIp)]
        public string HttpClientIp { get; set; }

        // Code origin tags (entry span only, frame 0).
        // Exit span code origin can include multiple frames and requires dynamic tag keys.
        // PDB-enriched tags (file/line/column) are not stored as properties to avoid adding extra fields to every
        // WebTags instance. When available, those tags are added via the tag list instead.
        [Tag("_dd.code_origin.type")]
        public string CodeOriginType { get; set; }

        [Tag("_dd.code_origin.frames.0.index")]
        public string CodeOriginFrames0Index { get; set; }

        [Tag("_dd.code_origin.frames.0.method")]
        public string CodeOriginFrames0Method { get; set; }

        [Tag("_dd.code_origin.frames.0.type")]
        public string CodeOriginFrames0Type { get; set; }
    }
}
