// <copyright file="WebTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class WebTags : InstrumentationTags, IHasStatusCode
    {
        protected static readonly IProperty<string>[] WebTagsProperties =
            InstrumentationTagsProperties.Concat(
                new Property<WebTags, string>(Trace.Tags.HttpStatusCode, t => t.HttpStatusCode, (t, v) => t.HttpStatusCode = v),
                new Property<WebTags, string>(Trace.Tags.HttpMethod, t => t.HttpMethod, (t, v) => t.HttpMethod = v),
                new Property<WebTags, string>(Trace.Tags.HttpRequestHeadersHost, t => t.HttpRequestHeadersHost, (t, v) => t.HttpRequestHeadersHost = v),
                new Property<WebTags, string>(Trace.Tags.HttpUrl, t => t.HttpUrl, (t, v) => t.HttpUrl = v),
                new ReadOnlyProperty<WebTags, string>(Trace.Tags.Language, t => t.Language));

        public override string SpanKind => SpanKinds.Server;

        public string HttpMethod { get; set; }

        public string HttpRequestHeadersHost { get; set; }

        public string HttpUrl { get; set; }

        public string Language => TracerConstants.Language;

        public string HttpStatusCode { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => WebTagsProperties;
    }
}
