// <copyright file="RunProcessResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner;

internal readonly struct RunProcessResult
{
    internal RunProcessResult(int exitCode, bool timedOut, int rootProcessId, bool treeKillAttempted, bool treeKillSucceeded, bool reaped)
    {
        ExitCode = exitCode;
        TimedOut = timedOut;
        RootProcessId = rootProcessId;
        TreeKillAttempted = treeKillAttempted;
        TreeKillSucceeded = treeKillSucceeded;
        Reaped = reaped;
    }

    internal int ExitCode { get; }

    internal bool TimedOut { get; }

    internal int RootProcessId { get; }

    internal bool TreeKillAttempted { get; }

    internal bool TreeKillSucceeded { get; }

    internal bool Reaped { get; }
}
