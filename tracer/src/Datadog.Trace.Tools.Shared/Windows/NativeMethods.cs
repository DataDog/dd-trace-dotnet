// <copyright file="NativeMethods.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Original code from https://github.com/gapotchenko/Gapotchenko.FX/tree/master/Source/Gapotchenko.FX.Diagnostics.Process
// MIT License
//
// Copyright © 2019 Gapotchenko and Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

#pragma warning disable SA1201 // Elements should appear in the correct order

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.Tools.Shared.Windows
{
    public static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_INFO
        {
            public ushort wProcessorArchitecture;
            public ushort wReserved;
            public uint dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
            public uint dwProcessorType;
            public uint dwAllocationGranularity;
            public ushort wProcessorLevel;
            public ushort wProcessorRevision;
        }

        [DllImport("kernel32.dll", ExactSpelling = true)]
        public static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        public static extern void GetNativeSystemInfo(out SYSTEM_INFO lpSystemInfo);

        // -------------------------------------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public IntPtr[] Reserved2;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        public const int ProcessBasicInformation = 0;
        public const int ProcessWow64Information = 26;

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtQueryInformationProcess(
            IntPtr hProcess,
            int pic,
            out PROCESS_BASIC_INFORMATION pbi,
            int cb,
            out int pSize);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtQueryInformationProcess(
            IntPtr hProcess,
            int pic,
            ref IntPtr pi,
            int cb,
            ref int pSize);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PROCESS_BASIC_INFORMATION_WOW64
        {
            public long Reserved1;
            public long PebBaseAddress;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public long[] Reserved2;
            public long UniqueProcessId;
            public long Reserved3;
        }

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtWow64QueryInformationProcess64(
            IntPtr hProcess,
            int pic,
            ref PROCESS_BASIC_INFORMATION_WOW64 pbi,
            int cb,
            ref int pSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(
          IntPtr hProcess,
          IntPtr lpBaseAddress,
          [Out] byte[] lpBuffer,
          IntPtr dwSize,
          ref IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(
          IntPtr hProcess,
          IntPtr lpBaseAddress,
          IntPtr lpBuffer,
          IntPtr dwSize,
          ref IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static unsafe extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            void* lpBuffer,
            IntPtr dwSize,
            ref IntPtr lpNumberOfBytesRead);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtWow64ReadVirtualMemory64(
            IntPtr hProcess,
            long lpBaseAddress,
            IntPtr lpBuffer,
            long dwSize,
            ref long lpNumberOfBytesRead);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtWow64ReadVirtualMemory64(
            IntPtr hProcess,
            long lpBaseAddress,
            [Out] byte[] lpBuffer,
            long dwSize,
            ref long lpNumberOfBytesRead);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static unsafe extern int NtWow64ReadVirtualMemory64(
            IntPtr hProcess,
            long lpBaseAddress,
            void* lpBuffer,
            long dwSize,
            ref long lpNumberOfBytesRead);

        public const int STATUS_SUCCESS = 0;

        public const int PAGE_NOACCESS = 0x01;
        public const int PAGE_EXECUTE = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public int AllocationProtect;
            public IntPtr RegionSize;
            public int State;
            public int Protect;
            public int Type;
        }

        [DllImport("kernel32")]
        public static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, ref MEMORY_BASIC_INFORMATION lpBuffer, int dwLength);

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION_WOW64
        {
            public long BaseAddress;
            public long AllocationBase;
            public int AllocationProtect;
            public long RegionSize;
            public int State;
            public int Protect;
            public int Type;
        }

        public enum MEMORY_INFORMATION_CLASS
        {
            MemoryBasicInformation
        }

        [DllImport("ntdll.dll")]
        public static extern int NtWow64QueryVirtualMemory64(
            IntPtr hProcess,
            long lpAddress,
            MEMORY_INFORMATION_CLASS memoryInformationClass,
            IntPtr lpBuffer, // MEMORY_BASIC_INFORMATION_WOW64, pointer must be 64-bit aligned
            long memoryInformationLength,
            ref long returnLength);

        [DllImport("kernel32.dll")]
        public static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

        // -------------------------------------------------------------------------------------------------------

        public const int ERROR_ACCESS_DENIED = 5;
        public const int ERROR_INVALID_HANDLE = 6;
        public const int ERROR_INVALID_PARAMETER = 87;

        public const int CTRL_C_EVENT = 0;
        public const int CTRL_BREAK_EVENT = 1;

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool GenerateConsoleCtrlEvent(int dwCtrlEvent, int dwProcessGroupId);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool FreeConsole();

        public delegate bool HANDLER_ROUTINE(int dwCtrlType);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool SetConsoleCtrlHandler(HANDLER_ROUTINE HandlerRoutine, bool Add);

        // -------------------------------------------------------------------------------------------------------

        public const int MAX_PATH = 260;

        public const int ERROR_INSUFFICIENT_BUFFER = 0x0000007A;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("Ole32")]
        public static extern int CoCreateInstance(
            in Guid rclsid,
            IntPtr pUnkOuter,
            int dwClsContext,
            in Guid riid,
            out IntPtr ppObj);
    }
}
