// <copyright file="NullDependencyTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Reflection;

namespace Datadog.Trace.Telemetry;

internal class NullDependencyTelemetryCollector : IDependencyTelemetryCollector
{
    public static NullDependencyTelemetryCollector Instance { get; } = new();

    public void AssemblyLoaded(Assembly assembly)
    {
    }

    public List<DependencyTelemetryData>? GetData() => null;

    public List<DependencyTelemetryData>? GetFullData() => null;
}
