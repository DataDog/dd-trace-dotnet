// <copyright file="AspNetCoreTags2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging;

internal partial class AspNetCoreTags2 : WebTags
{
    [Tag(Trace.Tags.AspNetCoreController)]
    public string? AspNetCoreController { get; set; }

    [Tag(Trace.Tags.AspNetCoreAction)]
    public string? AspNetCoreAction { get; set; }

    [Tag(Trace.Tags.AspNetCoreArea)]
    public string? AspNetCoreArea { get; set; }

    [Tag(Trace.Tags.AspNetCorePage)]
    public string? AspNetCorePage { get; set; }

    // Read/write instead of readonly as AzureFunctions updates the component name
    [Tag(Trace.Tags.InstrumentationName)]
    public string InstrumentationName => "aspnet_core";

    [Tag(Trace.Tags.AspNetCoreRoute)]
    public string? AspNetCoreRoute { get; set; }

    [Tag(Trace.Tags.AspNetCoreEndpoint)]
    public string? AspNetCoreEndpoint { get; set; }

    [Tag(Tags.HttpRoute)]
    public string? HttpRoute { get; set; }

    // TODO: merge with AspNetCoreRoute?
    public List<string>? SubsequentRoutes { get; set; }
}
