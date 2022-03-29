// <copyright file="HttpTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class HttpTags : InstrumentationTags, IHasStatusCode
    {
        private const string HttpClientHandlerTypeKey = "http-client-handler-type";

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName { get; set; }

        [Tag(Trace.Tags.HttpMethod)]
        public string HttpMethod { get; set; }

        [Tag(Trace.Tags.HttpUrl)]
        public string HttpUrl { get; set; }

        [Tag(HttpClientHandlerTypeKey)]
        public string HttpClientHandlerType { get; set; }

        [Tag(Trace.Tags.HttpStatusCode)]
        public string HttpStatusCode { get; set; }
    }
}
