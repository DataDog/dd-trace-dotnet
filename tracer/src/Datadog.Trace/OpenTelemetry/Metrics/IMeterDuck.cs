// <copyright file="IMeterDuck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.OpenTelemetry.Metrics;

/// <summary>
/// Duck type interface for accessing Meter.Tags (available in .NET 8+)
/// </summary>
[DuckType("System.Diagnostics.Metrics.Meter", "System.Diagnostics.DiagnosticSource", IncludeDerivedTypes = true)]
internal interface IMeterDuck
{
    IEnumerable<KeyValuePair<string, object?>>? Tags { get; }
}

#endif
