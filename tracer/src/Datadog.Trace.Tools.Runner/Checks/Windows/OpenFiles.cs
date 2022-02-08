// <copyright file="OpenFiles.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Original code taken from https://github.com/urosjovanovic/MceController/blob/master/VmcServices/DetectOpenFiles.cs

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
// ReSharper disable InconsistentNaming

namespace Datadog.Trace.Tools.Runner.Checks.Windows
{
    internal class OpenFiles
    {
        private const string NetworkDevicePrefix = "\\Device\\LanmanRedirector\\";
        private const int MaxPath = 260;
        private const int HandleTypeTokenCount = 27;

        private static readonly string[] HandleTypeTokens =
        {
            string.Empty, string.Empty, "Directory", "SymbolicLink", "Token",
            "Process", "Thread", "Unknown7", "Event", "EventPair", "Mutant",
            "Unknown11", "Semaphore", "Timer", "Profile", "WindowStation",
            "Desktop", "Section", "Key", "Port", "WaitablePort",
            "Unknown21", "Unknown22", "Unknown23", "Unknown24",
            "IoCompletion", "File"
        };

        private static Dictionary<string, string> _deviceMap;

        internal enum NT_STATUS
        {
            STATUS_SUCCESS = 0x00000000,
            STATUS_BUFFER_OVERFLOW = unchecked((int)0x80000005L),
            STATUS_INFO_LENGTH_MISMATCH = unchecked((int)0xC0000004L)
        }

        internal enum SYSTEM_INFORMATION_CLASS
        {
            SystemBasicInformation = 0,
            SystemPerformanceInformation = 2,
            SystemTimeOfDayInformation = 3,
            SystemProcessInformation = 5,
            SystemProcessorPerformanceInformation = 8,
            SystemHandleInformation = 16,
            SystemInterruptInformation = 23,
            SystemExceptionInformation = 33,
            SystemRegistryQuotaInformation = 37,
            SystemLookasideInformation = 45
        }

        internal enum OBJECT_INFORMATION_CLASS
        {
            ObjectBasicInformation = 0,
            ObjectNameInformation = 1,
            ObjectTypeInformation = 2,
            ObjectAllTypesInformation = 3,
            ObjectHandleInformation = 4
        }

        [Flags]
        internal enum ProcessAccessRights
        {
            PROCESS_DUP_HANDLE = 0x00000040
        }

        [Flags]
        internal enum DuplicateHandleOptions
        {
            DUPLICATE_CLOSE_SOURCE = 0x1,
            DUPLICATE_SAME_ACCESS = 0x2
        }

        private enum SystemHandleType
        {
            OB_TYPE_UNKNOWN = 0,
            OB_TYPE_TYPE = 1,
            OB_TYPE_DIRECTORY,
            OB_TYPE_SYMBOLIC_LINK,
            OB_TYPE_TOKEN,
            OB_TYPE_PROCESS,
            OB_TYPE_THREAD,
            OB_TYPE_UNKNOWN_7,
            OB_TYPE_EVENT,
            OB_TYPE_EVENT_PAIR,
            OB_TYPE_MUTANT,
            OB_TYPE_UNKNOWN_11,
            OB_TYPE_SEMAPHORE,
            OB_TYPE_TIMER,
            OB_TYPE_PROFILE,
            OB_TYPE_WINDOW_STATION,
            OB_TYPE_DESKTOP,
            OB_TYPE_SECTION,
            OB_TYPE_KEY,
            OB_TYPE_PORT,
            OB_TYPE_WAITABLE_PORT,
            OB_TYPE_UNKNOWN_21,
            OB_TYPE_UNKNOWN_22,
            OB_TYPE_UNKNOWN_23,
            OB_TYPE_UNKNOWN_24,
            // OB_TYPE_CONTROLLER,
            // OB_TYPE_DEVICE,
            // OB_TYPE_DRIVER,
            OB_TYPE_IO_COMPLETION,
            OB_TYPE_FILE
        }

        public static IEnumerable<string> GetOpenFiles(int processId)
        {
            NT_STATUS ret;
            int length = 0x10000;

            do
            {
                var ptr = IntPtr.Zero;

                try
                {
                    ptr = Marshal.AllocHGlobal(length);

                    ret = NativeMethods.NtQuerySystemInformation(SYSTEM_INFORMATION_CLASS.SystemHandleInformation, ptr, length, out var returnLength);

                    if (ret == NT_STATUS.STATUS_INFO_LENGTH_MISMATCH)
                    {
                        // Round required memory up to the nearest 64KB boundary.
                        length = ((returnLength + 0xffff) & ~0xffff);
                    }
                    else if (ret == NT_STATUS.STATUS_SUCCESS)
                    {
                        int handleCount = Marshal.ReadInt32(ptr);
                        int offset = sizeof(int);
                        int size = Marshal.SizeOf(typeof(SYSTEM_HANDLE_ENTRY));

                        for (int i = 0; i < handleCount; i++)
                        {
                            var handleEntry = (SYSTEM_HANDLE_ENTRY)Marshal.PtrToStructure(IntPtrAdd(ptr, offset), typeof(SYSTEM_HANDLE_ENTRY));
                            int ownerProcessId = GetProcessId(handleEntry.OwnerPid);
                            if (ownerProcessId == processId)
                            {
                                var handle = (IntPtr)handleEntry.HandleValue;

                                if (GetHandleType(handle, ownerProcessId, out var handleType) && handleType == SystemHandleType.OB_TYPE_FILE)
                                {
                                    if (GetFileNameFromHandle(handle, ownerProcessId, out var devicePath))
                                    {
                                        if (ConvertDevicePathToDosPath(devicePath, out var dosPath))
                                        {
                                            yield return dosPath;
                                        }
                                    }
                                }
                            }

                            offset += size;
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            while (ret == NT_STATUS.STATUS_INFO_LENGTH_MISMATCH);
        }

        private static IntPtr IntPtrAdd(IntPtr ptr, int offset)
        {
            if (IntPtr.Size == 4)
            {
                return (IntPtr)((int)ptr + offset);
            }

            return (IntPtr)((long)ptr + offset);
        }

        private static int GetProcessId(IntPtr processId)
        {
            if (IntPtr.Size == 4)
            {
                return (int)processId;
            }

            return (int)((long)processId >> 32);
        }

        private static bool GetFileNameFromHandle(IntPtr handle, int processId, out string fileName)
        {
            var currentProcess = NativeMethods.GetCurrentProcess();

            SafeProcessHandle processHandle = null;
            SafeObjectHandle objectHandle = null;

            try
            {
                processHandle = NativeMethods.OpenProcess(ProcessAccessRights.PROCESS_DUP_HANDLE, true, processId);

                if (NativeMethods.DuplicateHandle(processHandle.DangerousGetHandle(), handle, currentProcess, out objectHandle, 0, false, DuplicateHandleOptions.DUPLICATE_SAME_ACCESS))
                {
                    handle = objectHandle.DangerousGetHandle();
                }

                return GetFileNameFromHandle(handle, out fileName);
            }
            finally
            {
                processHandle?.Close();
                objectHandle?.Close();
            }
        }

        private static bool GetFileNameFromHandle(IntPtr handle, out string fileName)
        {
            var ptr = IntPtr.Zero;

            try
            {
                int length = 0x200;  // 512 bytes

                ptr = Marshal.AllocHGlobal(length);

                var ret = NativeMethods.NtQueryObject(handle, OBJECT_INFORMATION_CLASS.ObjectNameInformation, ptr, length, out length);

                if (ret == NT_STATUS.STATUS_BUFFER_OVERFLOW)
                {
                    Marshal.FreeHGlobal(ptr);
                    ptr = Marshal.AllocHGlobal(length);

                    ret = NativeMethods.NtQueryObject(handle, OBJECT_INFORMATION_CLASS.ObjectNameInformation, ptr, length, out length);
                }

                if (ret == NT_STATUS.STATUS_SUCCESS)
                {
                    var objNameInfo = (OBJECT_NAME_INFORMATION)Marshal.PtrToStructure(ptr, typeof(OBJECT_NAME_INFORMATION));
                    fileName = objNameInfo.Name.Buffer;
                    return fileName.Length != 0;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            fileName = string.Empty;
            return false;
        }

        private static bool GetHandleType(IntPtr handle, int processId, out SystemHandleType handleType)
        {
            var token = GetHandleTypeToken(handle, processId);
            return GetHandleTypeFromToken(token, out handleType);
        }

        private static bool GetHandleTypeFromToken(string token, out SystemHandleType handleType)
        {
            for (int i = 1; i < HandleTypeTokenCount; i++)
            {
                if (HandleTypeTokens[i] == token)
                {
                    handleType = (SystemHandleType)i;
                    return true;
                }
            }

            handleType = SystemHandleType.OB_TYPE_UNKNOWN;
            return false;
        }

        private static string GetHandleTypeToken(IntPtr handle, int processId)
        {
            var currentProcess = NativeMethods.GetCurrentProcess();
            SafeProcessHandle processHandle = null;
            SafeObjectHandle objectHandle = null;

            try
            {
                processHandle = NativeMethods.OpenProcess(ProcessAccessRights.PROCESS_DUP_HANDLE, true, processId);

                if (NativeMethods.DuplicateHandle(processHandle.DangerousGetHandle(), handle, currentProcess, out objectHandle, 0, false, DuplicateHandleOptions.DUPLICATE_SAME_ACCESS))
                {
                    handle = objectHandle.DangerousGetHandle();
                }

                return GetHandleTypeToken(handle);
            }
            finally
            {
                processHandle?.Close();
                objectHandle?.Close();
            }
        }

        private static string GetHandleTypeToken(IntPtr handle)
        {
            NativeMethods.NtQueryObject(handle, OBJECT_INFORMATION_CLASS.ObjectTypeInformation, IntPtr.Zero, 0, out var length);
            var ptr = IntPtr.Zero;

            try
            {
                ptr = Marshal.AllocHGlobal(length);

                if (NativeMethods.NtQueryObject(handle, OBJECT_INFORMATION_CLASS.ObjectTypeInformation, ptr, length, out length) == NT_STATUS.STATUS_SUCCESS)
                {
                    var objTypeInfo = (OBJECT_TYPE_INFORMATION)Marshal.PtrToStructure(ptr, typeof(OBJECT_TYPE_INFORMATION));
                    return objTypeInfo.TypeName.Buffer;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return string.Empty;
        }

        private static bool ConvertDevicePathToDosPath(string devicePath, out string dosPath)
        {
            EnsureDeviceMap();
            int i = devicePath.Length;

            while (i > 0 && (i = devicePath.LastIndexOf('\\', i - 1)) != -1)
            {
                if (_deviceMap.TryGetValue(devicePath.Substring(0, i), out var drive))
                {
                    dosPath = string.Concat(drive, devicePath.Substring(i));
                    return dosPath.Length != 0;
                }
            }

            dosPath = string.Empty;
            return false;
        }

        private static void EnsureDeviceMap()
        {
            if (_deviceMap == null)
            {
                var localDeviceMap = BuildDeviceMap();
                Interlocked.CompareExchange(ref _deviceMap, localDeviceMap, null);
            }
        }

        private static Dictionary<string, string> BuildDeviceMap()
        {
            var logicalDrives = Environment.GetLogicalDrives();
            var localDeviceMap = new Dictionary<string, string>(logicalDrives.Length);
            var lpTargetPath = new StringBuilder(MaxPath);

            foreach (var drive in logicalDrives)
            {
                var lpDeviceName = drive.Substring(0, 2);
                NativeMethods.QueryDosDevice(lpDeviceName, lpTargetPath, MaxPath);
                localDeviceMap.Add(NormalizeDeviceName(lpTargetPath.ToString()), lpDeviceName);
            }

            localDeviceMap.Add(NetworkDevicePrefix.Substring(0, NetworkDevicePrefix.Length - 1), "\\");
            return localDeviceMap;
        }

        private static string NormalizeDeviceName(string deviceName)
        {
            if (string.Compare(deviceName, 0, NetworkDevicePrefix, 0, NetworkDevicePrefix.Length, StringComparison.InvariantCulture) == 0)
            {
                var shareName = deviceName.Substring(deviceName.IndexOf('\\', NetworkDevicePrefix.Length) + 1);
                return NetworkDevicePrefix + shareName;
            }

            return deviceName;
        }

        // Modified from original solution based on https://stackoverflow.com/a/9995536
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SYSTEM_HANDLE_ENTRY
        {
            public IntPtr OwnerPid;
            public byte ObjectType;
            public byte HandleFlags;
            public short HandleValue;
            public IntPtr ObjectPointer;
            public int AccessMask;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string Buffer;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct OBJECT_TYPE_INFORMATION
        {
            public UNICODE_STRING TypeName;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
            public ulong[] Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct OBJECT_NAME_INFORMATION
        {
            public UNICODE_STRING Name;
            public IntPtr NameBuffer;
        }

        internal static class NativeMethods
        {
            [DllImport("ntdll.dll")]
            internal static extern NT_STATUS NtQuerySystemInformation(
                [In] SYSTEM_INFORMATION_CLASS SystemInformationClass,
                [In] IntPtr SystemInformation,
                [In] int SystemInformationLength,
                [Out] out int ReturnLength);

            [DllImport("ntdll.dll")]
            internal static extern NT_STATUS NtQueryObject(
                [In] IntPtr Handle,
                [In] OBJECT_INFORMATION_CLASS ObjectInformationClass,
                [In] IntPtr ObjectInformation,
                [In] int ObjectInformationLength,
                [Out] out int ReturnLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern SafeProcessHandle OpenProcess(
                [In] ProcessAccessRights dwDesiredAccess,
                [In, MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
                [In] int dwProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool DuplicateHandle(
                [In] IntPtr hSourceProcessHandle,
                [In] IntPtr hSourceHandle,
                [In] IntPtr hTargetProcessHandle,
                [Out] out SafeObjectHandle lpTargetHandle,
                [In] int dwDesiredAccess,
                [In, MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
                [In] DuplicateHandleOptions dwOptions);

            [DllImport("kernel32.dll")]
            internal static extern IntPtr GetCurrentProcess();

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern int GetProcessId(
                [In] IntPtr Process);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(
                [In] IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern int QueryDosDevice(
                [In] string lpDeviceName,
                [Out] StringBuilder lpTargetPath,
                [In] int ucchMax);
        }

        internal sealed class SafeObjectHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            private SafeObjectHandle()
                : base(true)
            {
            }

            internal SafeObjectHandle(IntPtr preexistingHandle, bool ownsHandle)
                : base(ownsHandle)
            {
                SetHandle(preexistingHandle);
            }

            protected override bool ReleaseHandle()
            {
                return NativeMethods.CloseHandle(handle);
            }
        }

        internal sealed class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            private SafeProcessHandle()
                : base(true)
            {
            }

            internal SafeProcessHandle(IntPtr preexistingHandle, bool ownsHandle)
                : base(ownsHandle)
            {
                SetHandle(preexistingHandle);
            }

            protected override bool ReleaseHandle()
            {
                return NativeMethods.CloseHandle(handle);
            }
        }
    }
}
