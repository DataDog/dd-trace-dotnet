// <copyright file="AspNetCoreBlazorTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging;

internal partial class AspNetCoreBlazorTags : CommonTags
{
    [Tag(Trace.Tags.SpanKind)]
    public string SpanKind => SpanKinds.Server;

    [Tag(Trace.Tags.InstrumentationName)]
    public string InstrumentationName => "blazor";

    [Tag("razor_component")]
    public string Component { get; set; }

    [Tag("event_type")]
    public string EventType { get; set; }

    [Tag("connection_id")]
    public string ConnectionId { get; set; }
}
