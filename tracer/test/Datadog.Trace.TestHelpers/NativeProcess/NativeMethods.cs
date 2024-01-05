// <copyright file="NativeMethods.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Datadog.Trace.TestHelpers.NativeProcess;

internal class NativeMethods
{
    internal const int DUPLICATE_SAME_ACCESS = 2;
    internal const int STD_INPUT_HANDLE = -10;
    internal const int STD_OUTPUT_HANDLE = -11;
    internal const int STD_ERROR_HANDLE = -12;
    internal const int STARTF_USESTDHANDLES = 0x00000100;
    internal const int CREATE_NO_WINDOW = 0x08000000;
    internal const int CREATE_SUSPENDED = 0x00000004;
    internal const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    internal const int ERROR_BAD_EXE_FORMAT = 193;
    internal const int ERROR_EXE_MACHINE_TYPE_MISMATCH = 216;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool DuplicateHandle(
        IntPtr hSourceProcessHandle,
        SafeHandle hSourceHandle,
        IntPtr hTargetProcess,
        out SafeFileHandle targetHandle,
        int dwDesiredAccess,
        bool bInheritHandle,
        int dwOptions);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GetStdHandle(int whichHandle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetEnvironmentStrings();

    [DllImport("kernel32.dll")]
    internal static extern uint ResumeThread(SafeHandle hThread);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreateProcess(
        string? lpApplicationName,
        string? lpCommandLine,
        ref SECURITY_ATTRIBUTES procSecAttrs,
        ref SECURITY_ATTRIBUTES threadSecAttrs,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        int dwCreationFlags,
        string? lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        ref PROCESS_INFORMATION lpProcessInformation);

    [StructLayout(LayoutKind.Sequential)]
    internal struct STARTUPINFO
    {
        internal int cb;
        internal IntPtr lpReserved;
        internal IntPtr lpDesktop;
        internal IntPtr lpTitle;
        internal int dwX;
        internal int dwY;
        internal int dwXSize;
        internal int dwYSize;
        internal int dwXCountChars;
        internal int dwYCountChars;
        internal int dwFillAttribute;
        internal int dwFlags;
        internal short wShowWindow;
        internal short cbReserved2;
        internal IntPtr lpReserved2;
        internal IntPtr hStdInput;
        internal IntPtr hStdOutput;
        internal IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
        internal IntPtr hProcess;
        internal IntPtr hThread;
        internal int dwProcessId;
        internal int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SECURITY_ATTRIBUTES
    {
        internal uint nLength;
        internal IntPtr lpSecurityDescriptor;
        internal int bInheritHandle;
    }
}
