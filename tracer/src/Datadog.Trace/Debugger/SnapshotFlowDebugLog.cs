// <copyright file="SnapshotFlowDebugLog.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Debugger;

/// <summary>
/// Centralized gate for the internal snapshot flow debug logs.
/// Keeping the switch here makes the extra logging easy to remove without changing behavior.
/// </summary>
internal static class SnapshotFlowDebugLog
{
    // Temporary debug/development escape hatch for snapshot exploration work.
    // This env var is intentionally kept out of supported configuration metadata and telemetry normalization.
    // We keep it for now to make investigation easy, and it should be safe to remove once the workflow stabilizes.
    private const string EnabledEnvVar = "DD_INTERNAL_DEBUGGER_SNAPSHOT_FLOW_LOGS";

    // Debug-only exploration knobs are intentionally kept out of supported configuration metadata.
#pragma warning disable DD0012
    private static readonly bool IsFlagEnabled =
        string.Equals(EnvironmentHelpers.GetEnvironmentVariable(EnabledEnvVar), "1", StringComparison.Ordinal);
#pragma warning restore DD0012

    internal static bool IsEnabled(IDatadogLogger log)
    {
        return IsFlagEnabled && log.IsEnabled(LogEventLevel.Debug);
    }
}
