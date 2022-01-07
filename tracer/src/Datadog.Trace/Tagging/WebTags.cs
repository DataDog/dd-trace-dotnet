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

        [Tag(Trace.Tags.HttpMethod)]
        public string HttpMethod { get; set; }

        [Tag(Trace.Tags.HttpRequestHeadersHost)]
        public string HttpRequestHeadersHost { get; set; }

        [Tag(Trace.Tags.HttpUrl)]
        public string HttpUrl { get; set; }

        [Tag(Trace.Tags.Language)]
        public string Language => TracerConstants.Language;

        [Tag(Trace.Tags.HttpStatusCode)]
        public string HttpStatusCode { get; set; }

        [Tag(Trace.Tags.Env)]
        public string DummyTag { get; set; }
    }
}
