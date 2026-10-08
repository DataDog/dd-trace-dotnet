// <copyright file="AotLog.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

/// <summary>
/// Minimal log for the offline instrumentation: warnings and errors always go to stderr, debug only when verbose.
/// </summary>
internal static class AotLog
{
    private static readonly object Sync = new();

    public static bool Verbose { get; set; }

    public static Action<string, string>? Sink { get; set; }

    public static void Info(string message) => Write("INF", message, force: true);

    public static void Debug(string message) => Write("DBG", message, force: false);

    public static void Warn(string message) => Write("WRN", message, force: true);

    public static void Error(string message) => Write("ERR", message, force: true);

    private static void Write(string level, string message, bool force)
    {
        if (!force && !Verbose)
        {
            return;
        }

        lock (Sync)
        {
            if (Sink is { } sink)
            {
                sink(level, message);
            }
            else
            {
                Console.Error.WriteLine($"[{level}] {message}");
            }
        }
    }
}
