// <copyright file="AspNetCoreSingleSpanTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging;

internal partial class AspNetCoreSingleSpanTags : WebTags
{
    private const string ComponentName = "aspnet_core";

    [Tag(Trace.Tags.InstrumentationName)]
    public string InstrumentationName { get; } = ComponentName;

    [Tag(Trace.Tags.AspNetCoreRoute)]
    public string? AspNetCoreRoute { get; set; }

    [Tag(Tags.HttpRoute)]
    public string? HttpRoute { get; set; }

    [Tag(Trace.Tags.AspNetCoreEndpoint)]
    public string? AspNetCoreEndpoint { get; set; }

    // TODO: Additional executed endpoints e.g. multiple pipeline executions
    // Error paths etc
}
