// <copyright file="GcPauseTimeReflection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Reflection;
using Datadog.Trace.Logging;

namespace Datadog.Trace.RuntimeMetrics;

/// <summary>
/// Shared helper for resolving <c>GC.GetTotalPauseDuration()</c> via reflection.
/// Used by both the OTLP polyfill and the DogStatsD runtime metrics listener on .NET 6–8.
/// Each caller is responsible for converting the <see cref="TimeSpan"/> to its preferred unit
/// (seconds for OTel, milliseconds for DogStatsD).
/// </summary>
internal static class GcPauseTimeReflection
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GcPauseTimeReflection));

    /// <summary>
    /// Returns a delegate for <c>GC.GetTotalPauseDuration()</c>, or <c>null</c> if the method
    /// is not available on the current runtime (i.e. .NET versions older than 6.0.21).
    /// </summary>
    /// <remarks>
    /// <c>GC.GetTotalPauseDuration()</c> was introduced in .NET 6.0.21:
    /// https://github.com/dotnet/runtime/pull/87143
    /// This is also what the OTel runtime instrumentation package uses:
    /// https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/5aa6d868/src/OpenTelemetry.Instrumentation.Runtime/RuntimeMetrics.cs#L105C40-L107
    /// We use reflection rather than duck typing because this is a simple static method with no overloads.
    /// </remarks>
    public static Func<TimeSpan>? TryCreateDelegate()
    {
        var version = FrameworkDescription.Instance.RuntimeVersion;
        if (version.Major <= 6 && version is not { Major: 6, Build: >= 21 })
        {
            return null;
        }

        var methodInfo = typeof(GC).GetMethod("GetTotalPauseDuration", BindingFlags.Public | BindingFlags.Static);
        if (methodInfo is null)
        {
            Log.Debug("GC.GetTotalPauseDuration() is not available on this runtime version; gc pause time will not be reported.");
            return null;
        }

        return methodInfo.CreateDelegate<Func<TimeSpan>>();
    }
}

#endif
