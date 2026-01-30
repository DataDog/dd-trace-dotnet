// <copyright file="InferredProxyTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging;

internal sealed partial class InferredProxyTags : InstrumentationTags, IHasStatusCode
{
    [Tag(Trace.Tags.SpanKind)]
    public override string SpanKind => SpanKinds.Server;

    [Tag(Trace.Tags.InstrumentationName)]
    public string? InstrumentationName { get; set; }

    [Tag(Trace.Tags.HttpMethod)]
    public string? HttpMethod { get; set; }

    [Tag(Trace.Tags.HttpUrl)]
    public string? HttpUrl { get; set; }

    [Tag(Trace.Tags.HttpRoute)]
    public string? HttpRoute { get; set; }

    [Tag(Trace.Tags.HttpStatusCode)]
    public string? HttpStatusCode { get; set; }

    [Tag(Trace.Tags.ProxyStage)]
    public string? Stage { get; set; }

    [Metric(Metrics.InferredSpan)]
    public double? InferredSpan { get; set; }
}
