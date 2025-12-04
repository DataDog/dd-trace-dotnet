// <copyright file="HttpTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal abstract partial class HttpTags : InstrumentationTags, IHasStatusCode
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;

        public abstract string InstrumentationName { get; set; }

        public abstract string HttpMethod { get; set; }

        public abstract string HttpUrl { get; set; }

        public abstract string HttpStatusCode { get; set; }

        public abstract string Host { get; set; }
    }
}
