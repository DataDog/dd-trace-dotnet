// <copyright file="SuspendedProcess.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Datadog.Trace.TestHelpers.NativeProcess;

internal class SuspendedProcess(int pid, SafeHandle threadHandle, StreamWriter? standardInput, StreamReader? standardOutput, StreamReader? standardError) : IDisposable
{
    private readonly SafeHandle _threadHandle = threadHandle;

    private readonly StreamWriter? _standardInput = standardInput;
    private readonly StreamReader? _standardOutput = standardOutput;
    private readonly StreamReader? _standardError = standardError;

    public int Id { get; } = pid;

    public Process ResumeProcess()
    {
        NativeMethods.ResumeThread(_threadHandle);

        var process = Process.GetProcessById(Id);

        if (_standardInput != null)
        {
            typeof(Process).GetField("_standardInput", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(process, _standardInput);
        }

        if (_standardOutput != null)
        {
            typeof(Process).GetField("_standardOutput", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(process, _standardOutput);
        }

        if (_standardError != null)
        {
            typeof(Process).GetField("_standardError", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(process, _standardError);
        }

        return process;
    }

    public void Dispose()
    {
        _threadHandle.Dispose();
    }
}
