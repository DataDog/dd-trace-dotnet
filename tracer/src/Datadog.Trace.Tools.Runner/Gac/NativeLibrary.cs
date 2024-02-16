// <copyright file="NativeLibrary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Gac;

#if !NETCOREAPP3_0_OR_GREATER

internal sealed class NativeLibrary
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string libName);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    public static IntPtr Load(string libraryPath)
    {
        var value = LoadLibrary(libraryPath);
        if (value != IntPtr.Zero)
        {
            return value;
        }

        var errorCode = Marshal.GetLastWin32Error();
        var hr = Marshal.GetHRForLastWin32Error();
        throw new Exception($"Failed to load library (ErrorCode: {errorCode}, HR: {hr})");
    }

    public static IntPtr GetExport(IntPtr handle, string name)
    {
        var value = GetProcAddress(handle, name);
        if (value != IntPtr.Zero)
        {
            return value;
        }

        var errorCode = Marshal.GetLastWin32Error();
        var hr = Marshal.GetHRForLastWin32Error();
        throw new Exception($"Failed to get the Export (ErrorCode: {errorCode}, HR: {hr})");
    }

    public static void Free(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        FreeLibrary(handle);
    }
}

#endif
