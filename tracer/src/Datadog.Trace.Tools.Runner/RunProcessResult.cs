// <copyright file="RunProcessResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner;

internal readonly struct RunProcessResult
{
    public RunProcessResult(int exitCode, bool timedOut, int rootProcessId, bool treeKillAttempted, bool treeKillSucceeded, bool reaped)
    {
        ExitCode = exitCode;
        TimedOut = timedOut;
        RootProcessId = rootProcessId;
        TreeKillAttempted = treeKillAttempted;
        TreeKillSucceeded = treeKillSucceeded;
        Reaped = reaped;
    }

    public int ExitCode { get; }

    public bool TimedOut { get; }

    public int RootProcessId { get; }

    public bool TreeKillAttempted { get; }

    public bool TreeKillSucceeded { get; }

    public bool Reaped { get; }
}
