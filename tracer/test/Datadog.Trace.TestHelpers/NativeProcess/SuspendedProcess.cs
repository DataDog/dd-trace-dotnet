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
using Microsoft.Win32.SafeHandles;

namespace Datadog.Trace.TestHelpers.NativeProcess;

internal class SuspendedProcess(ProcessStartInfo startInfo, int pid, SafeProcessHandle processHandle, SafeHandle threadHandle, StreamWriter? standardInput, StreamReader? standardOutput, StreamReader? standardError) : IDisposable
{
#if NETFRAMEWORK
    private const string StandardInputFieldName = "standardInput";
    private const string StandardOutputFieldName = "standardOutput";
    private const string StandardErrorFieldName = "standardError";
#else
    private const string StandardInputFieldName = "_standardInput";
    private const string StandardOutputFieldName = "_standardOutput";
    private const string StandardErrorFieldName = "_standardError";
#endif

    private readonly ProcessStartInfo _startInfo = startInfo;

    private readonly SafeProcessHandle _processHandle = processHandle;
    private readonly SafeHandle _threadHandle = threadHandle;

    private readonly StreamWriter? _standardInput = standardInput;
    private readonly StreamReader? _standardOutput = standardOutput;
    private readonly StreamReader? _standardError = standardError;

    private bool _isResumed;
    private bool _isDisposed;

    public int Id { get; } = pid;

    public Process ResumeProcess()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(SuspendedProcess));
        }

        if (_isResumed)
        {
            throw new InvalidOperationException("You really, really shouldn't call ResumeProcess twice");
        }

        _isResumed = true;

        NativeMethods.ResumeThread(_threadHandle);

        var process = new Process { StartInfo = _startInfo };

        typeof(Process).GetMethod("SetProcessHandle", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(process, [_processHandle]);
        typeof(Process).GetMethod("SetProcessId", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(process, [Id]);

        if (_standardInput != null)
        {
            typeof(Process).GetField(StandardInputFieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(process, _standardInput);
        }

        if (_standardOutput != null)
        {
            typeof(Process).GetField(StandardOutputFieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(process, _standardOutput);
        }

        if (_standardError != null)
        {
            typeof(Process).GetField(StandardErrorFieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(process, _standardError);
        }

        return process;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _threadHandle.Dispose();

            // If ResumeProcess has been called, those resources are now owned by the returned Process object
            if (!_isResumed)
            {
                _processHandle.Dispose();
                _standardError?.Dispose();
                _standardInput?.Dispose();
                _standardOutput?.Dispose();
            }
        }
    }
}
