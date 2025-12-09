// <copyright file="MutexHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Util;

internal static class MutexHelper
{
    /// <summary>
    /// Wait for the <paramref name="mutex"/> to be set for the specified <paramref name="timeout"/>.
    /// If the mutex is not set, take a memory dump, and return the result
    /// </summary>
    /// <param name="mutex">The mutex to wait for</param>
    /// <param name="timeout">How long to wait for the mutex to be set, in ms</param>
    /// <param name="output">Optional output parameter</param>
    /// <param name="caller">The caller, populated automatically</param>
    public static bool WaitOrDump(this ManualResetEventSlim mutex, int timeout, ITestOutputHelper output = null, [CallerMemberName] string caller = null)
        => DoWaitOrDump(mutex, TimeSpan.FromMilliseconds(timeout), output, caller);

    /// <summary>
    /// Wait for the <paramref name="mutex"/> to be set for the specified <paramref name="timeout"/>.
    /// If the mutex is not set, take a memory dump, and return the result
    /// </summary>
    /// <param name="mutex">The mutex to wait for</param>
    /// <param name="timeout">How long to wait for the mutex to be set, in ms</param>
    /// <param name="output">Optional output parameter</param>
    /// <param name="caller">The caller, populated automatically</param>
    public static bool WaitOrDump(this ManualResetEventSlim mutex, TimeSpan timeout, ITestOutputHelper output = null, [CallerMemberName] string caller = null)
        => DoWaitOrDump(mutex, timeout, output, caller);

    private static bool DoWaitOrDump(ManualResetEventSlim mutex, TimeSpan timeout, ITestOutputHelper output, string caller)
    {
        var result = mutex.Wait(timeout);
        if (!result)
        {
            output?.WriteLine($"Mutex timed out in {caller}. Capturing memory dump");
            var process = Process.GetCurrentProcess();
            var log = MemoryDumpHelper.CaptureMemoryDump(process)
                          ? "Successfully captured memory dump"
                          : "Failed to capture memory dump";
            output?.WriteLine(log);
        }

        return result;
    }
}
