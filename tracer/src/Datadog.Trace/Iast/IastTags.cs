// <copyright file="IastTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Iast;

internal partial class IastTags : CommonTags
{
    [Tag(Tags.IastJson)]
    public string? IastJson { get; set; }

    [Tag(Tags.IastJsonTagSizeExceeded)]
    public string? IastJsonTagSizeExceeded { get; set; }

    [Tag(Tags.IastEnabled)]
    public string? IastEnabled { get; set; }
}
