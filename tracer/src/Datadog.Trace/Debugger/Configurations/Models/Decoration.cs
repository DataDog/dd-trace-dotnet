// <copyright file="Decoration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1402 // FileMayOnlyContainASingleType - StyleCop did not enforce this for records initially

#nullable enable
namespace Datadog.Trace.Debugger.Configurations.Models;

internal record Decoration
{
    public SnapshotSegment? When { get; set; }

    public Tags[]? Tags { get; set; }
}

internal record Tags
{
    public string? Name { get; set; }

    public TagValue? Value { get; set; }
}

internal record TagValue
{
    public string? Template { get; set; }

    public SnapshotSegment[]? Segments { get; set; }
}

#pragma warning restore SA1402 // FileMayOnlyContainASingleType - StyleCop did not enforce this for records initially
