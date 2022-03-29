// <copyright file="NativeInterop.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable SA1402 // File may only contain a single type   | helper namespace with marshalling related types
#pragma warning disable SA1202 // Elements should be ordered by access  | prefer to group methods by topic
namespace Datadog.Profiler
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0049:Simplify Names", Justification = "Interop class uses Framework types names instead of Language names.")]
    internal static class NativeInterop
    {
        // For .NET FRAMEWORK, we have to specify the file extension of the native library.
        // We run on Windows only, so the extension is always ".dll", and we can justspecify it.
        //
        // For .NET CORE, we don't have to specify the file extension of the native library.
        // So, we let DllImport/LoadLibrary add the extension according to the OS:
        //     Windows -> ".dll"
        //     Linux -> ".so"
        //     MacOS -> ".dylib"
#if NETFRAMEWORK
        private const string NativeProfilerEngineLibNameX86 = "Datadog.AutoInstrumentation.Profiler.Native.x86.dll";
        private const string NativeProfilerEngineLibNameX64 = "Datadog.AutoInstrumentation.Profiler.Native.x64.dll";
#else
        private const string NativeProfilerEngineLibNameX86 = "Datadog.AutoInstrumentation.Profiler.Native.x86";
        private const string NativeProfilerEngineLibNameX64 = "Datadog.AutoInstrumentation.Profiler.Native.x64";
#endif

        // --------------------------------------------------------
        // PInvokes into 3rd party DLLs
        [DllImport(dllName: "psapi.dll", EntryPoint = "GetModuleBaseNameW", CharSet = CharSet.Unicode)]
        private static extern UInt32 GetModuleBaseNameW_any(IntPtr processHandle, IntPtr moduleHandle, [Out] StringBuilder moduleBaseName, UInt32 nameBuffSize);

        public static bool TryGetModuleBaseName(UInt64 moduleHandle, out string moduleBaseName)
        {
            const Int64 ThisProcessHandle = -1;
            const int NameBuffSize = 1024;

            if (moduleHandle == 0)
            {
                moduleBaseName = null;
                return false;
            }

            var buffer = new StringBuilder(capacity: NameBuffSize);
            UInt32 nameLen = GetModuleBaseNameW_any((IntPtr)ThisProcessHandle, (IntPtr)moduleHandle, buffer, (UInt32)buffer.Capacity);

            if (nameLen < 1)
            {
                moduleBaseName = null;
                return false;
            }
            else
            {
                moduleBaseName = buffer.ToString();
                return true;
            }
        }

        // Note that the defaut calling convention is StdCall. That is what we use.
        // Although this is already the default, we still explicitly specify it for readability.

        // --------------------------------------------------------
        // TryCompleteCurrentWriteSegment
        public static bool TryCompleteCurrentWriteSegment()
        {
            bool success = false;
            if (Environment.Is64BitProcess)
            {
                if (!TryCompleteCurrentWriteSegment_x64(ref success))
                {
                    ClrShutdownException.Throw("TryCompleteCurrentWriteSegment is called AFTER CLR shutdown");
                }

                return success;
            }
            else
            {
                if (!TryCompleteCurrentWriteSegment_x86(ref success))
                {
                    ClrShutdownException.Throw("TryCompleteCurrentWriteSegment is called AFTER CLR shutdown");
                }

                return success;
            }
        }

        [DllImport(
            dllName: NativeProfilerEngineLibNameX86,
            EntryPoint = "TryCompleteCurrentWriteSegment",
            CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)] // default value, but use the attribute to make it explicit that it is a 4 bytes integer value (i.e. C BOOL on the native side)
        private static extern bool TryCompleteCurrentWriteSegment_x86(ref bool success);

        [DllImport(
            dllName: NativeProfilerEngineLibNameX64,
            EntryPoint = "TryCompleteCurrentWriteSegment",
            CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)] // default value, but use the attribute to make it explicit that it is a 4 bytes integer value (i.e. C BOOL on the native side)
        private static extern bool TryCompleteCurrentWriteSegment_x64(ref bool success);

        // --------------------------------------------------------
        // TryMakeSegmentAvailableForWrite
        public static bool TryMakeSegmentAvailableForWrite(IntPtr segment)
        {
            bool isReleased = false;
            if (Environment.Is64BitProcess)
            {
                if (!TryMakeSegmentAvailableForWrite_x64(segment, ref isReleased))
                {
                    ClrShutdownException.Throw("TryMakeSegmentAvailableForWrite is called AFTER CLR shutdown");
                }

                return isReleased;
            }
            else
            {
                if (!TryMakeSegmentAvailableForWrite_x86(segment, ref isReleased))
                {
                    ClrShutdownException.Throw("TryMakeSegmentAvailableForWrite is called AFTER CLR shutdown");
                }

                return isReleased;
            }
        }

        [DllImport(
            dllName: NativeProfilerEngineLibNameX86,
            EntryPoint = "TryMakeSegmentAvailableForWrite",
            CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)] // default value, but use the attribute to make it explicit that it is a 4 bytes integer value (i.e. C BOOL on the native side)
        private static extern bool TryMakeSegmentAvailableForWrite_x86(IntPtr segment, ref bool isReleased);

        [DllImport(
            dllName: NativeProfilerEngineLibNameX64,
            EntryPoint = "TryMakeSegmentAvailableForWrite",
            CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)] // default value, but use the attribute to make it explicit that it is a 4 bytes integer value (i.e. C BOOL on the native side)
        private static extern bool TryMakeSegmentAvailableForWrite_x64(IntPtr segment, ref bool isReleased);

        // --------------------------------------------------------
        // DebugDumpAllSnapshots
        [DllImport(
            dllName: NativeProfilerEngineLibNameX86,
            EntryPoint = "DebugDumpAllSnapshots",
            CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)] // default value, but use the attribute to make it explicit that it is a 4 bytes integer value (i.e. C BOOL on the native side)
        private static extern bool DebugDumpAllSnapshots_x86(IntPtr segmentNativeObjectPtr);

        [DllImport(
            dllName: NativeProfilerEngineLibNameX64,
            EntryPoint = "DebugDumpAllSnapshots",
            CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)] // default value, but use the attribute to make it explicit that it is a 4 bytes integer value (i.e. C BOOL on the native side)
        private static extern bool DebugDumpAllSnapshots_x64(IntPtr segmentNativeObjectPtr);

        public static bool DebugDumpAllSnapshots(IntPtr segmentNativeObjectPtr)
        {
            if (Environment.Is64BitProcess)
            {
                return DebugDumpAllSnapshots_x64(segmentNativeObjectPtr);
            }
            else
            {
                return DebugDumpAllSnapshots_x86(segmentNativeObjectPtr);
            }
        }

        // --------------------------------------------------------
        // TryResolveStackFrameSymbols
        [DllImport(
            dllName: NativeProfilerEngineLibNameX86,
            EntryPoint = "TryResolveStackFrameSymbols",
            CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)] // default value, but use the attribute to make it explicit that it is a 4 bytes integer value (i.e. C BOOL on the native side)
        private static extern bool TryResolveStackFrameSymbols_x86(
                                        StackFrameCodeKind frameCodeKind,
                                        UInt64 frameInfoCode,
                                        ref IntPtr pFunctionName,
                                        ref IntPtr pContainingTypeName,
                                        ref IntPtr pContainingAssemblyName);

        [DllImport(
            dllName: NativeProfilerEngineLibNameX64,
            EntryPoint = "TryResolveStackFrameSymbols",
            CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)] // default value, but use the attribute to make it explicit that it is a 4 bytes integer value (i.e. C BOOL on the native side)
        private static extern bool TryResolveStackFrameSymbols_x64(
                                        StackFrameCodeKind frameCodeKind,
                                        UInt64 frameInfoCode,
                                        ref IntPtr pFunctionName,
                                        ref IntPtr pContainingTypeName,
                                        ref IntPtr pContainingAssemblyName);

        public static void TryResolveStackFrameSymbols(
                                StackFrameCodeKind frameCodeKind,
                                UInt64 frameInfoCode,
                                ref IntPtr pFunctionName,
                                ref IntPtr pContainingTypeName,
                                ref IntPtr pContainingAssemblyName)
        {
            if (Environment.Is64BitProcess)
            {
                if (!TryResolveStackFrameSymbols_x64(frameCodeKind, frameInfoCode, ref pFunctionName, ref pContainingTypeName, ref pContainingAssemblyName))
                {
                    ClrShutdownException.Throw("TryResolveStackFrameSymbols is called AFTER CLR shutdown");
                }
            }
            else
            {
                if (!TryResolveStackFrameSymbols_x86(frameCodeKind, frameInfoCode, ref pFunctionName, ref pContainingTypeName, ref pContainingAssemblyName))
                {
                    ClrShutdownException.Throw("TryResolveStackFrameSymbols is called AFTER CLR shutdown");
                }
            }
        }

        // --------------------------------------------------------
        // TryResolveAppDomainInfoSymbols
        // The below may need to be refactored to use char* or similar instead of string builder if the string builder is always copied.
        // Monitor this issue: https://github.com/dotnet/runtime/issues/47735
        // Possible perf improvement like here: https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1838
        [DllImport(
            dllName: NativeProfilerEngineLibNameX86,
            EntryPoint = "TryResolveAppDomainInfoSymbols",
            CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)] // default value, but use the attribute to make it explicit that it is a 4 bytes integer value (i.e. C BOOL on the native side)
        private static extern bool TryResolveAppDomainInfoSymbols_x86(
                                        UInt64 appDomainId,
                                        UInt32 appDomainNameBuffSize,
                                        ref UInt32 actualAppDomainNameLen,
                                        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder appDomainNameBuff,
                                        ref UInt64 appDomainProcessId,
                                        ref bool success);

        [DllImport(
            dllName: NativeProfilerEngineLibNameX64,
            EntryPoint = "TryResolveAppDomainInfoSymbols",
            CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)] // default value, but use the attribute to make it explicit that it is a 4 bytes integer value (i.e. C BOOL on the native side)
        private static extern bool TryResolveAppDomainInfoSymbols_x64(
                                        UInt64 profilerAppDomainId,
                                        UInt32 appDomainNameBuffSize,
                                        ref UInt32 actualAppDomainNameLen,
                                        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder appDomainNameBuff,
                                        ref UInt64 appDomainProcessId,
                                        ref bool success);

        public static bool ResolveAppDomainInfoSymbols(
                                UInt64 profilerAppDomainId,
                                UInt32 appDomainNameBuffSize,
                                ref UInt32 actualAppDomainNameLen,
                                StringBuilder appDomainNameBuff,
                                ref UInt64 appDomainProcessId)
        {
            bool success = false;
            if (Environment.Is64BitProcess)
            {
                if (!TryResolveAppDomainInfoSymbols_x64(
                        profilerAppDomainId,
                        appDomainNameBuffSize,
                        ref actualAppDomainNameLen,
                        appDomainNameBuff,
                        ref appDomainProcessId,
                        ref success))
                {
                    ClrShutdownException.Throw("TryResolveAppDomainInfoSymbols is called AFTER CLR shutdown");
                }

                return success;
            }
            else
            {
                if (!TryResolveAppDomainInfoSymbols_x86(
                        profilerAppDomainId,
                        appDomainNameBuffSize,
                        ref actualAppDomainNameLen,
                        appDomainNameBuff,
                        ref appDomainProcessId,
                        ref success))
                {
                    ClrShutdownException.Throw("TryResolveAppDomainInfoSymbols is called AFTER CLR shutdown");
                }

                return success;
            }
        }

        // --------------------------------------------------------
        // TryGetThreadInfo
        // The below may need to be refactored to use char* or similar instead of string builder if the string builder is always copied.
        // Monitor this issue: https://github.com/dotnet/runtime/issues/47735
        // Possible perf improvement like here: https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1838
        [DllImport(
            dllName: NativeProfilerEngineLibNameX86,
            EntryPoint = "TryGetThreadInfo",
            CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)] // default value, but use the attribute to make it explicit that it is a 4 bytes integer value (i.e. C BOOL on the native side)
        private static extern bool TryGetThreadInfo_x86(
                                        UInt32 profilerThreadInfoId,
                                        ref UInt64 clrThreadId,
                                        ref UInt32 osThreadId,
                                        ref IntPtr osThreadHandle,
                                        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder threadNameBuff,
                                        UInt32 threadNameBuffSize,
                                        ref UInt32 actualThreadNameLen,
                                        ref bool success);

        [DllImport(
            dllName: NativeProfilerEngineLibNameX64,
            EntryPoint = "TryGetThreadInfo",
            CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)] // default value, but use the attribute to make it explicit that it is a 4 bytes integer value (i.e. C BOOL on the native side)
        private static extern bool TryGetThreadInfo_x64(
                                        UInt32 profilerThreadInfoId,
                                        ref UInt64 clrThreadId,
                                        ref UInt32 osThreadId,
                                        ref IntPtr osThreadHandle,
                                        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder threadNameBuff,
                                        UInt32 threadNameBuffSize,
                                        ref UInt32 actualThreadNameLen,
                                        ref bool success);

        public static bool GetThreadInfo(
                            UInt32 profilerThreadInfoId,
                            ref UInt64 clrThreadId,
                            ref UInt32 osThreadId,
                            ref IntPtr osThreadHandle,
                            StringBuilder threadNameBuff,
                            UInt32 threadNameBuffSize,
                            ref UInt32 actualThreadNameLen)
        {
            bool success = false;
            if (Environment.Is64BitProcess)
            {
                if (!TryGetThreadInfo_x64(
                        profilerThreadInfoId,
                        ref clrThreadId,
                        ref osThreadId,
                        ref osThreadHandle,
                        threadNameBuff,
                        threadNameBuffSize,
                        ref actualThreadNameLen,
                        ref success))
                {
                    ClrShutdownException.Throw("TryGetThreadInfo");
                }
            }
            else
            {
                if (!TryGetThreadInfo_x86(
                        profilerThreadInfoId,
                        ref clrThreadId,
                        ref osThreadId,
                        ref osThreadHandle,
                        threadNameBuff,
                        threadNameBuffSize,
                        ref actualThreadNameLen,
                        ref success))
                {
                    ClrShutdownException.Throw("TryGetThreadInfo");
                }
            }

            return success;
        }

        // --------------------------------------------------------
        // ThreadsCpuManager_Map
        [DllImport(
            dllName: NativeProfilerEngineLibNameX86,
            EntryPoint = "ThreadsCpuManager_Map",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void ThreadsCpuManager_Map_x86(Int32 threadOSId, [MarshalAs(UnmanagedType.LPWStr)] string name);

        [DllImport(
            dllName: NativeProfilerEngineLibNameX64,
            EntryPoint = "ThreadsCpuManager_Map",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void ThreadsCpuManager_Map_x64(Int32 threadOSId, [MarshalAs(UnmanagedType.LPWStr)] string name);

        public static void ThreadsCpuManager_Map(
            Int32 threadOSId, [MarshalAs(UnmanagedType.LPWStr)] string name)
        {
            if (Environment.Is64BitProcess)
            {
                ThreadsCpuManager_Map_x64(threadOSId, name);
            }
            else
            {
                ThreadsCpuManager_Map_x86(threadOSId, name);
            }
        }

        // --------------------------------------------------------
        // ManagedCallbackRegistry
        public static class ManagedCallbackRegistry
        {
            public static class EnqueueStackSnapshotBufferSegmentForExport
            {
                [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false)]
                public delegate UInt32 Delegate_t(
                                    IntPtr segmentNativeObjectPtr,
                                    IntPtr segmentStartAddress,
                                    UInt32 segmentByteCount,
                                    UInt32 segmentSnapshotCount,
                                    UInt64 segmentUnixTimeUtcRangeStart,
                                    UInt64 segmentUnixTimeUtcRangeEnd);

                public static Delegate_t Set(Delegate_t callback)
                {
                    if (Environment.Is64BitProcess)
                    {
                        return ManagedCallbackRegistry_EnqueueStackSnapshotBufferSegmentForExport_Set_x64(callback);
                    }
                    else
                    {
                        return ManagedCallbackRegistry_EnqueueStackSnapshotBufferSegmentForExport_Set_x86(callback);
                    }
                }

                [DllImport(
                    dllName: NativeProfilerEngineLibNameX86,
                    EntryPoint = "ManagedCallbackRegistry_EnqueueStackSnapshotBufferSegmentForExport_Set",
                    CallingConvention = CallingConvention.StdCall)]
                [return: MarshalAs(UnmanagedType.FunctionPtr)]
                private static extern Delegate_t ManagedCallbackRegistry_EnqueueStackSnapshotBufferSegmentForExport_Set_x86(
                                                                                        [MarshalAs(UnmanagedType.FunctionPtr)] Delegate_t callback);

                [DllImport(
                    dllName: NativeProfilerEngineLibNameX64,
                    EntryPoint = "ManagedCallbackRegistry_EnqueueStackSnapshotBufferSegmentForExport_Set",
                    CallingConvention = CallingConvention.StdCall)]
                [return: MarshalAs(UnmanagedType.FunctionPtr)]
                private static extern Delegate_t ManagedCallbackRegistry_EnqueueStackSnapshotBufferSegmentForExport_Set_x64(
                                                                                        [MarshalAs(UnmanagedType.FunctionPtr)] Delegate_t callback);
            }

            // --------------------------------------------------------
            // TryShutdownCurrentManagedProfilerEngine
            public static class TryShutdownCurrentManagedProfilerEngine
            {
                [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false)]
                public delegate bool Delegate_t();

                public static Delegate_t Set(Delegate_t callback)
                {
                    if (Environment.Is64BitProcess)
                    {
                        return ManagedCallbackRegistry_TryShutdownCurrentManagedProfilerEngine_Set_x64(callback);
                    }
                    else
                    {
                        return ManagedCallbackRegistry_TryShutdownCurrentManagedProfilerEngine_Set_x86(callback);
                    }
                }

                [DllImport(
                    dllName: NativeProfilerEngineLibNameX86,
                    EntryPoint = "ManagedCallbackRegistry_TryShutdownCurrentManagedProfilerEngine_Set",
                    CallingConvention = CallingConvention.StdCall)]
                [return: MarshalAs(UnmanagedType.FunctionPtr)]
                private static extern Delegate_t ManagedCallbackRegistry_TryShutdownCurrentManagedProfilerEngine_Set_x86(
                                                                                        [MarshalAs(UnmanagedType.FunctionPtr)] Delegate_t callback);

                [DllImport(
                    dllName: NativeProfilerEngineLibNameX64,
                    EntryPoint = "ManagedCallbackRegistry_TryShutdownCurrentManagedProfilerEngine_Set",
                    CallingConvention = CallingConvention.StdCall)]
                [return: MarshalAs(UnmanagedType.FunctionPtr)]
                private static extern Delegate_t ManagedCallbackRegistry_TryShutdownCurrentManagedProfilerEngine_Set_x64(
                                                                                        [MarshalAs(UnmanagedType.FunctionPtr)] Delegate_t callback);
            }

            // --------------------------------------------------------
            // SetCurrentManagedThreadName
            public static class SetCurrentManagedThreadName
            {
                [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false)]
                public delegate UInt32 Delegate_t(IntPtr pThreadNameCharArr);

                public static Delegate_t Set(Delegate_t callback)
                {
                    if (Environment.Is64BitProcess)
                    {
                        return ManagedCallbackRegistry_SetCurrentManagedThreadName_Set_x64(callback);
                    }
                    else
                    {
                        return ManagedCallbackRegistry_SetCurrentManagedThreadName_Set_x86(callback);
                    }
                }

                [DllImport(
                    dllName: NativeProfilerEngineLibNameX86,
                    EntryPoint = "ManagedCallbackRegistry_SetCurrentManagedThreadName_Set",
                    CallingConvention = CallingConvention.StdCall)]
                [return: MarshalAs(UnmanagedType.FunctionPtr)]
                private static extern Delegate_t ManagedCallbackRegistry_SetCurrentManagedThreadName_Set_x86(
                                                                                        [MarshalAs(UnmanagedType.FunctionPtr)] Delegate_t callback);

                [DllImport(
                    dllName: NativeProfilerEngineLibNameX64,
                    EntryPoint = "ManagedCallbackRegistry_SetCurrentManagedThreadName_Set",
                    CallingConvention = CallingConvention.StdCall)]
                [return: MarshalAs(UnmanagedType.FunctionPtr)]
                private static extern Delegate_t ManagedCallbackRegistry_SetCurrentManagedThreadName_Set_x64(
                                                                                        [MarshalAs(UnmanagedType.FunctionPtr)] Delegate_t callback);
            }
        }
    }

    internal class ClrShutdownException : Exception
    {
        private ClrShutdownException(string message)
            : base(message)
        {
        }

        public static void Throw(string message)
        {
            throw new ClrShutdownException(message);
        }
    }
}
#pragma warning restore SA1402 // File may only contain a single type
#pragma warning restore SA1202 // Elements should be ordered by access
