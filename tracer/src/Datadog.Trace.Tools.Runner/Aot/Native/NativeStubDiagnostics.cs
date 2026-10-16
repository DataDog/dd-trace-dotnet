// <copyright file="NativeStubDiagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

/// <summary>
/// Collects the emulated CLR APIs the native tracer called but the emulation does not implement, and the exceptions
/// raised while serving native calls (which are never allowed to cross back into native code).
/// </summary>
internal static class NativeStubDiagnostics
{
    private static readonly ConcurrentDictionary<string, int> NotImplementedCalls = new();
    private static readonly ConcurrentQueue<string> Exceptions = new();

    public static IReadOnlyDictionary<string, int> NotImplementedCallCounts => NotImplementedCalls;

    public static IReadOnlyCollection<string> Errors => Exceptions;

    public static void OnException(string member, Exception exception)
    {
        Exceptions.Enqueue($"{member}: {exception}");
        AotLog.Error($"Exception while serving {member}: {exception}");
    }

    public static void NotImplemented(string member)
    {
        if (NotImplementedCalls.AddOrUpdate(member, 1, static (_, value) => value + 1) == 1)
        {
            AotLog.Debug($"Not implemented: {member}");
        }
    }

    public static void Reset()
    {
        NotImplementedCalls.Clear();
        while (Exceptions.TryDequeue(out _))
        {
        }
    }
}
#endif
