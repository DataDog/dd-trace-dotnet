// <copyright file="ProcessEnvironmentWindows.cs" company="Datadog">
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

#pragma warning disable SA1300 // Element should begin with upper-case letter

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Datadog.Trace.Tools.Shared.Windows
{
    public static class ProcessEnvironmentWindows
    {
        public static IReadOnlyDictionary<string, string> ReadVariables(Process process)
        {
            return _ReadVariablesCore(process);
        }

        public static int GetProcessBitness(Process process)
        {
            return _GetProcessBitness(process.Handle);
        }

        private static Dictionary<string, string> _ReadVariablesCore(Process process)
        {
            int retryCount = 5;
            bool RetryPolicy() => --retryCount > 0;

        Again:
            try
            {
                var stream = _GetEnvStream(process.Handle);
                var reader = new ProcessBinaryReader(new BufferedStream(stream), Encoding.Unicode);
                var env = _ReadEnv(reader);

                if (env.Count == 0)
                {
                    // Empty environment may indicate that a process environment block has not been initialized yet.
                    if (RetryPolicy())
                    {
                        goto Again;
                    }
                }

                return env;
            }
            catch (EndOfStreamException ex)
            {
                if (process.HasExited)
                {
                    throw new InvalidOperationException("The target process has exited", ex);
                }

                // There may be a race condition in environment block initialization of a recently started process.
                if (RetryPolicy())
                {
                    Thread.Sleep(1000);
                    goto Again;
                }
                else
                {
                    throw;
                }
            }
        }

        private static Stream _GetEnvStream(IntPtr hProcess)
        {
            var penv = _GetPenv(hProcess);
            if (penv.CanBeRepresentedByNativePointer)
            {
                int dataSize;
                if (!_HasReadAccess(hProcess, penv, out dataSize))
                {
                    throw new Exception("Unable to read environment block.");
                }

                dataSize = _ClampEnvSize(dataSize);

                var adapter = new ProcessMemoryAdapter(hProcess);
                return new ProcessMemoryStream(adapter, penv, dataSize);
            }
            else if (penv.Size == 8 && IntPtr.Size == 4)
            {
                // Accessing a 64-bit process from 32-bit host.

                int dataSize;
                try
                {
                    if (!_HasReadAccessWow64(hProcess, penv.ToInt64(), out dataSize))
                    {
                        throw new Exception("Unable to read environment block with WOW64 API.");
                    }
                }
                catch (EntryPointNotFoundException)
                {
                    // Windows 10 does not provide NtWow64QueryVirtualMemory64 API call.
                    dataSize = -1;
                }

                dataSize = _ClampEnvSize(dataSize);

                var adapter = new ProcessMemoryAdapterWow64(hProcess);
                return new ProcessMemoryStream(adapter, penv, dataSize);
            }
            else
            {
                throw new Exception("Unable to access process memory due to unsupported bitness cardinality.");
            }
        }

        private static int _ClampEnvSize(int size)
        {
            int maxSize = EnvironmentInfo.MaxSize;

            if (maxSize != -1)
            {
                if (size == -1 || size > maxSize)
                {
                    size = maxSize;
                }
            }

            return size;
        }

        private static Dictionary<string, string> _ReadEnv(ProcessBinaryReader br)
        {
            // Environment variables are case insensitive on Windows
            var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                var s = br.ReadCString();
                if (s.Length == 0)
                {
                    // End of environment block.
                    break;
                }

                int j = s.IndexOf('=');
                if (j <= 0)
                {
                    continue;
                }

                string name = s.Substring(0, j);
                string value = s.Substring(j + 1);

                env[name] = value;
            }

            return env;
        }

        private static bool _TryReadIntPtr32(IntPtr hProcess, IntPtr ptr, out IntPtr readPtr)
        {
            bool result;
#pragma warning disable CS0618, SYSLIB0004 // Type or member is obsolete
            RuntimeHelpers.PrepareConstrainedRegions();
#pragma warning restore CS0618, SYSLIB0004 // Type or member is obsolete
            try
            {
            }
            finally
            {
                int dataSize = sizeof(int);
                var data = Marshal.AllocHGlobal(dataSize);
                var res_len = IntPtr.Zero;
                bool b = NativeMethods.ReadProcessMemory(
                    hProcess,
                    ptr,
                    data,
                    new IntPtr(dataSize),
                    ref res_len);
                readPtr = new IntPtr(Marshal.ReadInt32(data));
                Marshal.FreeHGlobal(data);
                if (!b || (int)res_len != dataSize)
                {
                    result = false;
                }
                else
                {
                    result = true;
                }
            }

            return result;
        }

        private static bool _TryReadIntPtr(IntPtr hProcess, IntPtr ptr, out IntPtr readPtr)
        {
            bool result;
#pragma warning disable CS0618, SYSLIB0004 // Type or member is obsolete
            RuntimeHelpers.PrepareConstrainedRegions();
#pragma warning restore CS0618, SYSLIB0004 // Type or member is obsolete
            try
            {
            }
            finally
            {
                int dataSize = IntPtr.Size;
                var data = Marshal.AllocHGlobal(dataSize);
                var res_len = IntPtr.Zero;
                bool b = NativeMethods.ReadProcessMemory(
                    hProcess,
                    ptr,
                    data,
                    new IntPtr(dataSize),
                    ref res_len);
                readPtr = Marshal.ReadIntPtr(data);
                Marshal.FreeHGlobal(data);
                if (!b || (int)res_len != dataSize)
                {
                    result = false;
                }
                else
                {
                    result = true;
                }
            }

            return result;
        }

        private static bool _TryReadIntPtrWow64(IntPtr hProcess, long ptr, out long readPtr)
        {
            bool result;
#pragma warning disable CS0618, SYSLIB0004 // Type or member is obsolete
            RuntimeHelpers.PrepareConstrainedRegions();
#pragma warning restore CS0618, SYSLIB0004 // Type or member is obsolete
            try
            {
            }
            finally
            {
                int dataSize = sizeof(long);
                var data = Marshal.AllocHGlobal(dataSize);
                long res_len = 0;
                int status = NativeMethods.NtWow64ReadVirtualMemory64(
                    hProcess,
                    ptr,
                    data,
                    dataSize,
                    ref res_len);
                readPtr = Marshal.ReadInt64(data);
                Marshal.FreeHGlobal(data);
                if (status != NativeMethods.STATUS_SUCCESS || res_len != dataSize)
                {
                    result = false;
                }
                else
                {
                    result = true;
                }
            }

            return result;
        }

        private static UniPtr _GetPenv(IntPtr hProcess)
        {
            int processBitness = _GetProcessBitness(hProcess);

            if (processBitness == 64)
            {
                if (Environment.Is64BitProcess)
                {
                    // Accessing a 64-bit process from 64-bit host.

                    IntPtr pPeb = _GetPeb64(hProcess);

                    if (!_TryReadIntPtr(hProcess, pPeb + 0x20, out var ptr))
                    {
                        throw new Exception("Unable to read PEB.");
                    }

                    if (!_TryReadIntPtr(hProcess, ptr + 0x80, out var penv))
                    {
                        throw new Exception("Unable to read RTL_USER_PROCESS_PARAMETERS.");
                    }

                    return penv;
                }
                else
                {
                    // Accessing a 64-bit process from 32-bit host.

                    var pPeb = _GetPeb64(hProcess);

                    if (!_TryReadIntPtrWow64(hProcess, pPeb.ToInt64() + 0x20, out var ptr))
                    {
                        throw new Exception("Unable to read PEB.");
                    }

                    if (!_TryReadIntPtrWow64(hProcess, ptr + 0x80, out var penv))
                    {
                        throw new Exception("Unable to read RTL_USER_PROCESS_PARAMETERS.");
                    }

                    return new UniPtr(penv);
                }
            }
            else
            {
                // Accessing a 32-bit process from 32-bit host.

                IntPtr pPeb = _GetPeb32(hProcess);

                if (!_TryReadIntPtr32(hProcess, pPeb + 0x10, out var ptr))
                {
                    throw new Exception("Unable to read PEB.");
                }

                if (!_TryReadIntPtr32(hProcess, ptr + 0x48, out var penv))
                {
                    throw new Exception("Unable to read RTL_USER_PROCESS_PARAMETERS.");
                }

                return penv;
            }
        }

        private static int _GetProcessBitness(IntPtr hProcess)
        {
            if (Environment.Is64BitOperatingSystem)
            {
                if (!NativeMethods.IsWow64Process(hProcess, out var wow64))
                {
                    return 32;
                }

                if (wow64)
                {
                    return 32;
                }

                return 64;
            }
            else
            {
                return 32;
            }
        }

        private static IntPtr _GetPeb32(IntPtr hProcess)
        {
            if (Environment.Is64BitProcess)
            {
                var ptr = IntPtr.Zero;
                int res_len = 0;
                int pbiSize = IntPtr.Size;
                int status = NativeMethods.NtQueryInformationProcess(
                    hProcess,
                    NativeMethods.ProcessWow64Information,
                    ref ptr,
                    pbiSize,
                    ref res_len);

                if (res_len != pbiSize)
                {
                    throw new Exception("Unable to query process information.");
                }

                return ptr;
            }
            else
            {
                return _GetPebNative(hProcess);
            }
        }

        private static IntPtr _GetPebNative(IntPtr hProcess)
        {
            var pbiSize = Marshal.SizeOf<NativeMethods.PROCESS_BASIC_INFORMATION>();

            int status = NativeMethods.NtQueryInformationProcess(
                hProcess,
                NativeMethods.ProcessBasicInformation,
                out var pbi,
                pbiSize,
                out var res_len);

            if (res_len != pbiSize)
            {
                throw new Exception("Unable to query process information.");
            }

            return pbi.PebBaseAddress;
        }

        private static UniPtr _GetPeb64(IntPtr hProcess)
        {
            if (Environment.Is64BitProcess)
            {
                return _GetPebNative(hProcess);
            }
            else
            {
                // Get PEB via WOW64 API.
                var pbi = new NativeMethods.PROCESS_BASIC_INFORMATION_WOW64();
                int res_len = 0;
                int pbiSize = Marshal.SizeOf(pbi);
                int status = NativeMethods.NtWow64QueryInformationProcess64(
                    hProcess,
                    NativeMethods.ProcessBasicInformation,
                    ref pbi,
                    pbiSize,
                    ref res_len);

                if (res_len != pbiSize)
                {
                    throw new Exception("Unable to query process information.");
                }

                return new UniPtr(pbi.PebBaseAddress);
            }
        }

        private static bool _HasReadAccess(IntPtr hProcess, IntPtr address, out int size)
        {
            size = 0;

            var memInfo = new NativeMethods.MEMORY_BASIC_INFORMATION();
            int result = NativeMethods.VirtualQueryEx(
                hProcess,
                address,
                ref memInfo,
                Marshal.SizeOf(memInfo));

            if (result == 0)
            {
                return false;
            }

            if (memInfo.Protect == NativeMethods.PAGE_NOACCESS || memInfo.Protect == NativeMethods.PAGE_EXECUTE)
            {
                return false;
            }

            try
            {
                size = Convert.ToInt32(memInfo.RegionSize.ToInt64() - (address.ToInt64() - memInfo.BaseAddress.ToInt64()));
            }
            catch (OverflowException)
            {
                return false;
            }

            if (size <= 0)
            {
                return false;
            }

            return true;
        }

        private static unsafe bool _HasReadAccessWow64(IntPtr hProcess, long address, out int size)
        {
            size = 0;

            NativeMethods.MEMORY_BASIC_INFORMATION_WOW64 memInfo;

#if NET45
            var memInfoType = typeof(NativeMethods.MEMORY_BASIC_INFORMATION_WOW64);
            int memInfoLength = Marshal.SizeOf(memInfoType);
#else
            int memInfoLength = memInfoLength = Marshal.SizeOf<NativeMethods.MEMORY_BASIC_INFORMATION_WOW64>();
#endif

            const int memInfoAlign = 8;

            long resultLength = 0;
            int result;

            IntPtr hMemInfo = Marshal.AllocHGlobal(memInfoLength + (memInfoAlign * 2));
            try
            {
                // Align to 64 bits.
                var hMemInfoAligned = new IntPtr(hMemInfo.ToInt64() & ~(memInfoAlign - 1L));

                result = NativeMethods.NtWow64QueryVirtualMemory64(
                    hProcess,
                    address,
                    NativeMethods.MEMORY_INFORMATION_CLASS.MemoryBasicInformation,
                    hMemInfoAligned,
                    memInfoLength,
                    ref resultLength);

                memInfo = *(NativeMethods.MEMORY_BASIC_INFORMATION_WOW64*)hMemInfoAligned;
            }
            finally
            {
                Marshal.FreeHGlobal(hMemInfo);
            }

            if (result != NativeMethods.STATUS_SUCCESS)
            {
                return false;
            }

            if (memInfo.Protect == NativeMethods.PAGE_NOACCESS || memInfo.Protect == NativeMethods.PAGE_EXECUTE)
            {
                return false;
            }

            try
            {
                size = Convert.ToInt32(memInfo.RegionSize - (address - memInfo.BaseAddress));
            }
            catch (OverflowException)
            {
                return false;
            }

            if (size <= 0)
            {
                return false;
            }

            return true;
        }
    }
}
