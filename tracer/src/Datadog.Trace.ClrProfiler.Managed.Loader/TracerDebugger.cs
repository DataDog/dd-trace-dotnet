// <copyright file="TracerDebugger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Managed.Loader;

// Based on: https://github.com/microsoft/vstest/blob/main/src/Microsoft.TestPlatform.Execution.Shared/DebuggerBreakpoint.cs#L25
internal static class TracerDebugger
{
    // Based on: https://github.com/microsoft/vstest/blob/main/src/Microsoft.TestPlatform.Execution.Shared/DebuggerBreakpoint.cs#L140
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void WaitForDebugger(string environmentVariable)
    {
        if (Debugger.IsAttached)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(environmentVariable))
        {
            return;
        }

        var value = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrEmpty(value) || !value.Equals("1", StringComparison.Ordinal))
        {
            return;
        }

        Console.WriteLine("Waiting for debugger attach...");
        StartupLogger.Log("Waiting for debugger attach...");
        var currentProcess = Process.GetCurrentProcess();
        Console.WriteLine("Process Id: {0}, Name: {1}", currentProcess.Id, currentProcess.ProcessName);
        StartupLogger.Log("Process Id: {0}, Name: {1}", currentProcess.Id, currentProcess.ProcessName);
        if (!Debugger.IsAttached)
        {
            Debugger.Launch();
        }

        while (!Debugger.IsAttached)
        {
            Task.Delay(1000).GetAwaiter().GetResult();
        }

        Console.WriteLine("Debugger attached.");
        StartupLogger.Log("Debugger attached.");
        Break();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Break()
    {
        Task.Delay(1000).GetAwaiter().GetResult();
        StartupLogger.Log("Breaking form: " + Environment.StackTrace);
        Debugger.Break();
    }
}
