// <copyright file="ProcessExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.TestHelpers;

public static class ProcessExtensions
{
    public static async Task<bool> WaitForExitAsync(this Process process, int milliseconds = Timeout.Infinite)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.Exited += (_, _) => tcs.TrySetResult(true);
        process.EnableRaisingEvents = true;

        if (milliseconds == Timeout.Infinite)
        {
            await tcs.Task;
            return true;
        }

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(milliseconds));

        return completedTask == tcs.Task;
    }
}
