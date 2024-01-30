// <copyright file="DiagnosticInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.Helpers;

internal sealed record DiagnosticInfo
{
    public DiagnosticInfo(DiagnosticDescriptor descriptor, Location? location)
        : this(descriptor, location is not null ? LocationInfo.CreateFrom(location) : null)
    {
    }

    public DiagnosticInfo(DiagnosticDescriptor descriptor, LocationInfo? location)
    {
        Descriptor = descriptor;
        Location = location;
    }

    public DiagnosticDescriptor Descriptor { get; }

    public LocationInfo? Location { get; }
}
