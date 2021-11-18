// <copyright file="ReducedRegistryAccess.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    // reduced because we only need to read one value.
    // a one trick poney
    internal static class ReducedRegistryAccess
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ReducedRegistryAccess));

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/win32/sysinfo/registry-value-types
        /// https://docs.microsoft.com/en-us/windows/desktop/api/Winreg/nf-winreg-reggetvaluea
        /// </summary>
        [Flags]
        private enum RFlags
        {
            /// <summary>
            /// Any - No type restriction. (0x0000ffff)
            /// </summary>
            Any = 65535,

            /// <summary>
            /// Restrict type to REG_NONE. (0x00000001)
            /// </summary>
            RegNone = 1,

            /// <summary>
            /// Do not automatically expand environment strings if the value is of type REG_EXPAND_SZ. (0x10000000)
            /// </summary>
            Noexpand = 268435456,

            /// <summary>
            /// Bytes - Restrict type to REG_BINARY. (0x00000008)
            /// </summary>
            RegBinary = 8,

            /// <summary>
            /// Int32 - Restrict type to 32-bit RRF_RT_REG_BINARY | RRF_RT_REG_DWORD. (0x00000018)
            /// </summary>
            Dword = 24,

            /// <summary>
            /// Int32 - Restrict type to REG_DWORD. (0x00000010)
            /// </summary>
            RegDword = 16,

            /// <summary>
            /// Int64 - Restrict type to 64-bit RRF_RT_REG_BINARY | RRF_RT_REG_DWORD. (0x00000048)
            /// </summary>
            Qword = 72,

            /// <summary>
            /// Int64 - Restrict type to REG_QWORD. (0x00000040)
            /// </summary>
            RegQword = 64,

            /// <summary>
            /// A null-terminated string.
            /// This will be either a Unicode or an ANSI string,
            /// depending on whether you use the Unicode or ANSI functions.
            /// Restrict type to REG_SZ. (0x00000002)
            /// </summary>
            RegSz = 2,

            /// <summary>
            /// A sequence of null-terminated strings, terminated by an empty string (\0).
            /// The following is an example:
            /// String1\0String2\0String3\0LastString\0\0
            /// The first \0 terminates the first string, the second to the last \0 terminates the last string,
            /// and the final \0 terminates the sequence. Note that the final terminator must be factored into the length of the string.
            /// Restrict type to REG_MULTI_SZ. (0x00000020)
            /// </summary>
            RegMultiSz = 32,

            /// <summary>
            /// A null-terminated string that contains unexpanded references to environment variables (for example, "%PATH%").
            /// It will be a Unicode or ANSI string depending on whether you use the Unicode or ANSI functions.
            /// To expand the environment variable references, use the ExpandEnvironmentStrings function.
            /// Restrict type to REG_EXPAND_SZ. (0x00000004)
            /// </summary>
            RegExpandSz = 4,

            /// <summary>
            /// If pvData is not NULL, set the contents of the buffer to zeroes on failure. (0x20000000)
            /// </summary>
            RrfZeroonfailure = 536870912
        }

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/win32/sysinfo/registry-value-types
        /// </summary>
        private enum RType
        {
            RegNone = 0,

            RegSz = 1,
            RegExpandSz = 2,
            RegMultiSz = 7,

            RegBinary = 3,
            RegDword = 4,
            RegQword = 11,

            RegQwordLittleEndian = 11,
            RegDwordLittleEndian = 4,
            RegDwordBigEndian = 5,

            RegLink = 6,
            RegResourceList = 8,
            RegFullResourceDescriptor = 9,
            RegResourceRequirementsList = 10,
        }

        private enum HKEY : uint
        {
            HKEY_CLASSES_ROOT = 0x80000000,
            HKEY_CURRENT_USER = 0x80000001,
            HKEY_LOCAL_MACHINE = 0x80000002,
            HKEY_USERS = 0x80000003,
            HKEY_PERFORMANCE_DATA = 0x80000004,
            HKEY_CURRENT_CONFIG = 0x80000005,
            HKEY_DYN_DATA = 0x80000006,
        }

        [DllImport("Advapi32.dll", EntryPoint = "RegGetValueW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegGetValue(HKEY hkey, string lpSubKey, string lpValue, RFlags dwFlags, out RType pdwType, IntPtr pvData, ref int pcbData);

        public static string ReadLocalMachineString(string key, string value)
        {
            int pcbData = 512;
            string result = null;

            var pvData = Marshal.AllocHGlobal(pcbData);
            try
            {
                var hresult = RegGetValue(HKEY.HKEY_LOCAL_MACHINE, key, value, RFlags.Any, out var _, pvData, ref pcbData);

                Log.Debug<int>("RegGetValue - read string data: {pcbData}", pcbData);

                if (hresult != 0)
                {
                    // warning as the call could fail because the key is missing, which is expected in many situations
                    Log.Warning("registring access for key: {Key} failed with 0x{HResult}", key, hresult.ToString("X8"));
                    return null;
                }

                result = Marshal.PtrToStringUni(pvData);
            }
            finally
            {
                Marshal.FreeHGlobal(pvData);
            }

            return result;
        }
    }
}
