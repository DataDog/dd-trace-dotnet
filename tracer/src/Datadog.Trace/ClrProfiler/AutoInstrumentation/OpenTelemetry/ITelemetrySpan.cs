// <copyright file="ITelemetrySpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.OpenTelemetry
{
    /// <summary>
    /// Ducktype for type OpenTelemetry.Trace.TelemetrySpan. The wrapped <c>Activity</c> is an
    /// <c>internal readonly</c> <em>field</em> (not a property) on TelemetrySpan, so we need
    /// <see cref="DuckFieldAttribute"/> to tell duck typing to read the field instead of looking
    /// for a property of the same name. Same pattern as <c>IApiBaggage.Baggage</c>.
    /// <para>
    /// Returned as <see cref="object"/> rather than <c>System.Diagnostics.Activity</c> so the duck
    /// type compiles on TFMs whose <c>System.Diagnostics.DiagnosticSource</c> reference predates
    /// the existence of Activity (net461 etc.); callers duck-cast the result to <c>IActivity5</c>.
    /// </para>
    /// </summary>
    internal interface ITelemetrySpan
    {
        [DuckField(Name = "Activity")]
        object? Activity { get; }
    }
}
