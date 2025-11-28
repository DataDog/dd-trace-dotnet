// <copyright file="Instrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler;

internal sealed class Instrumentation
{
    /// <summary>
    /// Gets whether automatic instrumentation is attached.
    /// Rewritten by the tracer to return false if automatic instrumentation is enabled.
    /// </summary>
    // [Instrumented] This is auto-rewritten, not instrumented with calltarget
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool IsManualInstrumentationOnly() => true;
}
