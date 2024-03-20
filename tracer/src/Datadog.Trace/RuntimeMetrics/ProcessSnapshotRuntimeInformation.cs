// <copyright file="ProcessSnapshotRuntimeInformation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.RuntimeMetrics;

// ReSharper disable InconsistentNaming UnusedMember.Local
internal class ProcessSnapshotRuntimeInformation
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ProcessSnapshotRuntimeInformation>();

    private enum PSS_PROCESS_FLAGS
    {
        PSS_PROCESS_FLAGS_NONE = 0x00000000,
        PSS_PROCESS_FLAGS_PROTECTED = 0x00000001,
        PSS_PROCESS_FLAGS_WOW64 = 0x00000002,
        PSS_PROCESS_FLAGS_RESERVED_03 = 0x00000004,
        PSS_PROCESS_FLAGS_RESERVED_04 = 0x00000008,
        PSS_PROCESS_FLAGS_FROZEN = 0x00000010
    }

    private enum PSS_QUERY_INFORMATION_CLASS
    {
        PSS_QUERY_PROCESS_INFORMATION = 0,
        PSS_QUERY_VA_CLONE_INFORMATION = 1,
        PSS_QUERY_AUXILIARY_PAGES_INFORMATION = 2,
        PSS_QUERY_VA_SPACE_INFORMATION = 3,
        PSS_QUERY_HANDLE_INFORMATION = 4,
        PSS_QUERY_THREAD_INFORMATION = 5,
        PSS_QUERY_HANDLE_TRACE_INFORMATION = 6,
        PSS_QUERY_PERFORMANCE_COUNTERS = 7
    }

    [Flags]
    private enum PSS_CAPTURE_FLAGS : uint
    {
        PSS_CAPTURE_NONE = 0x00000000,
        PSS_CAPTURE_VA_CLONE = 0x00000001,
        PSS_CAPTURE_RESERVED_00000002 = 0x00000002,
        PSS_CAPTURE_HANDLES = 0x00000004,
        PSS_CAPTURE_HANDLE_NAME_INFORMATION = 0x00000008,
        PSS_CAPTURE_HANDLE_BASIC_INFORMATION = 0x00000010,
        PSS_CAPTURE_HANDLE_TYPE_SPECIFIC_INFORMATION = 0x00000020,
        PSS_CAPTURE_HANDLE_TRACE = 0x00000040,
        PSS_CAPTURE_THREADS = 0x00000080,
        PSS_CAPTURE_THREAD_CONTEXT = 0x00000100,
        PSS_CAPTURE_THREAD_CONTEXT_EXTENDED = 0x00000200,
        PSS_CAPTURE_RESERVED_00000400 = 0x00000400,
        PSS_CAPTURE_VA_SPACE = 0x00000800,
        PSS_CAPTURE_VA_SPACE_SECTION_INFORMATION = 0x00001000,
        PSS_CREATE_BREAKAWAY_OPTIONAL = 0x04000000,
        PSS_CREATE_BREAKAWAY = 0x08000000,
        PSS_CREATE_FORCE_BREAKAWAY = 0x10000000,
        PSS_CREATE_USE_VM_ALLOCATIONS = 0x20000000,
        PSS_CREATE_MEASURE_PERFORMANCE = 0x40000000,
        PSS_CREATE_RELEASE_SECTION = 0x80000000
    }

    // The value of the current process handle on Windows is hardcoded to -1
    // https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getcurrentprocess#remarks
    private static IntPtr CurrentProcessHandle => new(-1);

    public static unsafe bool GetCurrentProcessMetrics(out TimeSpan userProcessorTime, out TimeSpan systemCpuTime, out int threadCount, out long privateMemorySize)
    {
        var snapshotHandle = IntPtr.Zero;

        try
        {
            var result = PssCaptureSnapshot(CurrentProcessHandle, PSS_CAPTURE_FLAGS.PSS_CAPTURE_THREADS, 0, out snapshotHandle);

            if (result != 0)
            {
                throw new Win32Exception(result, $"PssCaptureSnapshot failed with code {result}");
            }

            PSS_THREAD_INFORMATION threadInformation = default;

            result = PssQuerySnapshot(snapshotHandle, PSS_QUERY_INFORMATION_CLASS.PSS_QUERY_THREAD_INFORMATION, &threadInformation, Marshal.SizeOf<PSS_THREAD_INFORMATION>());

            if (result != 0)
            {
                throw new Win32Exception(result, $"PssQuerySnapshot with PSS_QUERY_THREAD_INFORMATION failed with code {result}");
            }

            threadCount = threadInformation.ThreadsCaptured;

            long userTime;
            long kernelTime;
            long memoryUsage;

            if (Environment.Is64BitProcess)
            {
                PSS_PROCESS_INFORMATION_64 processInformation;

                result = PssQuerySnapshot(snapshotHandle, PSS_QUERY_INFORMATION_CLASS.PSS_QUERY_PROCESS_INFORMATION, &processInformation, Marshal.SizeOf<PSS_PROCESS_INFORMATION_64>());

                if (result != 0)
                {
                    throw new Win32Exception(result, $"PssQuerySnapshot with PSS_QUERY_PROCESS_INFORMATION (64 bits) failed with code {result}");
                }

                userTime = (long)processInformation.UserTime;
                kernelTime = (long)processInformation.KernelTime;
                memoryUsage = (long)processInformation.PrivateUsage;
            }
            else
            {
                PSS_PROCESS_INFORMATION_32 processInformation;

                result = PssQuerySnapshot(snapshotHandle, PSS_QUERY_INFORMATION_CLASS.PSS_QUERY_PROCESS_INFORMATION, &processInformation, Marshal.SizeOf<PSS_PROCESS_INFORMATION_32>());

                if (result != 0)
                {
                    throw new Win32Exception(result, $"PssQuerySnapshot with PSS_QUERY_PROCESS_INFORMATION (32 bits) failed with code {result}");
                }

                userTime = (long)processInformation.UserTime;
                kernelTime = (long)processInformation.KernelTime;
                memoryUsage = (long)processInformation.PrivateUsage;
            }

            userProcessorTime = TimeSpan.FromTicks(userTime);
            systemCpuTime = TimeSpan.FromTicks(kernelTime);
            privateMemorySize = memoryUsage;
            return true;
        }
        finally
        {
            if (snapshotHandle != IntPtr.Zero)
            {
                var result = PssFreeSnapshot(CurrentProcessHandle, snapshotHandle);

                if (result != 0)
                {
                    Log.Error<IntPtr, int>("PssFreeSnapshot returned an error, the tracer might be leaking memory. Handle: {Handle}. Error code: {Result}.", snapshotHandle, result);
                }
            }
        }
    }

    [DllImport("kernel32.dll")]
    private static extern int PssCaptureSnapshot(IntPtr processHandle, PSS_CAPTURE_FLAGS captureFlags, int threadContextFlags, out IntPtr snapshotHandle);

    [DllImport("kernel32.dll")]
    private static extern int PssFreeSnapshot(IntPtr processHandle, IntPtr snapshotHandle);

    [DllImport("kernel32.dll")]
    private static extern unsafe int PssQuerySnapshot(IntPtr snapshotHandle, PSS_QUERY_INFORMATION_CLASS informationClass, void* buffer, int bufferLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct PSS_THREAD_INFORMATION
    {
        public int ThreadsCaptured;
        public int ContextLength;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private unsafe struct PSS_PROCESS_INFORMATION_64
    {
        public uint ExitStatus;
        public IntPtr PebBaseAddress;
        public nint AffinityMask;
        public int BasePriority;
        public uint ProcessId;
        public uint ParentProcessId;
        public PSS_PROCESS_FLAGS Flags;
        public ulong CreateTime;
        public ulong ExitTime;
        public ulong KernelTime;
        public ulong UserTime;
        public uint PriorityClass;
        public nint PeakVirtualSize;
        public nint VirtualSize;
        public uint PageFaultCount;
        public nint PeakWorkingSetSize;
        public nint WorkingSetSize;
        public nint QuotaPeakPagedPoolUsage;
        public nint QuotaPagedPoolUsage;
        public nint QuotaPeakNonPagedPoolUsage;
        public nint QuotaNonPagedPoolUsage;
        public nint PagefileUsage;
        public nint PeakPagefileUsage;
        public nuint PrivateUsage;
        public uint ExecuteFlags;
        public fixed char ImageFileName[260];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private unsafe struct PSS_PROCESS_INFORMATION_32
    {
        public uint ExitStatus;
        public IntPtr PebBaseAddress;
        public nint AffinityMask;
        public int BasePriority;
        public uint ProcessId;
        public uint ParentProcessId;
        public PSS_PROCESS_FLAGS Flags;
        public ulong CreateTime;
        public ulong ExitTime;
        public ulong KernelTime;
        public ulong UserTime;
        public uint PriorityClass;
        public nuint PeakVirtualSize;
        public nuint VirtualSize;
        public uint PageFaultCount;
        public nuint PeakWorkingSetSize;
        public nuint WorkingSetSize;
        public nint QuotaPeakPagedPoolUsage;
        public nint QuotaPagedPoolUsage;
        public nint QuotaPeakNonPagedPoolUsage;
        public nint QuotaNonPagedPoolUsage;
        public nint PagefileUsage;
        public nint PeakPagefileUsage;
        public nuint PrivateUsage;
        public uint ExecuteFlags;
        public fixed char ImageFileName[260];
    }
}
