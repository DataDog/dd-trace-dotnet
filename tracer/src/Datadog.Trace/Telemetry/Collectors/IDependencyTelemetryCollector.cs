// <copyright file="IDependencyTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Reflection;

namespace Datadog.Trace.Telemetry;

internal interface IDependencyTelemetryCollector
{
    /// <summary>
    /// Called when an assembly is loaded
    /// </summary>
    void AssemblyLoaded(Assembly assembly);

    /// <summary>
    /// Get the latest data to send to the intake.
    /// </summary>
    /// <returns>Null if there are no changes, or the collector is not yet initialized</returns>
    List<DependencyTelemetryData>? GetData();

    /// <summary>
    /// Gets all the assembly data recorded so far
    /// </summary>
    List<DependencyTelemetryData>? GetFullData();
}
