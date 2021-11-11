// <copyright file="NativeCallTargetDefinition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.ClrProfiler
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NativeCallTargetDefinition
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetAssembly;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetType;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetMethod;

        public IntPtr TargetSignatureTypes;

        public ushort TargetSignatureTypesLength;

        [MarshalAs(UnmanagedType.U1)]
        public bool UseTargetMethodArgumentsToLoad;

        public IntPtr TargetMethodArgumentsToLoad;

        public ushort TargetMethodArgumentsToLoadLength;

        public ushort TargetMinimumMajor;

        public ushort TargetMinimumMinor;

        public ushort TargetMinimumPatch;

        public ushort TargetMaximumMajor;

        public ushort TargetMaximumMinor;

        public ushort TargetMaximumPatch;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string WrapperAssembly;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string WrapperType;

        public NativeCallTargetDefinition(
                string targetAssembly,
                string targetType,
                string targetMethod,
                string[] targetSignatureTypes,
                ushort[] targetMethodArgumentsToLoad,
                ushort targetMinimumMajor,
                ushort targetMinimumMinor,
                ushort targetMinimumPatch,
                ushort targetMaximumMajor,
                ushort targetMaximumMinor,
                ushort targetMaximumPatch,
                string wrapperAssembly,
                string wrapperType)
        {
            TargetAssembly = targetAssembly;
            TargetType = targetType;
            TargetMethod = targetMethod;
            TargetSignatureTypes = IntPtr.Zero;
            if (targetSignatureTypes?.Length > 0)
            {
                TargetSignatureTypes = Marshal.AllocHGlobal(targetSignatureTypes.Length * Marshal.SizeOf(typeof(IntPtr)));
                var ptr = TargetSignatureTypes;
                for (var i = 0; i < targetSignatureTypes.Length; i++)
                {
                    Marshal.WriteIntPtr(ptr, Marshal.StringToHGlobalUni(targetSignatureTypes[i]));
                    ptr += Marshal.SizeOf(typeof(IntPtr));
                }
            }

            TargetSignatureTypesLength = (ushort)(targetSignatureTypes?.Length ?? 0);

            UseTargetMethodArgumentsToLoad = targetMethodArgumentsToLoad is not null;
            TargetMethodArgumentsToLoad = IntPtr.Zero;
            if (targetMethodArgumentsToLoad?.Length > 0)
            {
                // The Marshal operations only operate on short not ushort (CLS-compliance)
                TargetMethodArgumentsToLoad = Marshal.AllocHGlobal(targetMethodArgumentsToLoad.Length * Marshal.SizeOf(typeof(short)));
                var ptr = TargetMethodArgumentsToLoad;
                for (var i = 0; i < targetMethodArgumentsToLoad.Length; i++)
                {
                    Marshal.WriteInt16(ptr, (short)targetMethodArgumentsToLoad[i]);
                    ptr += Marshal.SizeOf(typeof(short));
                }
            }

            TargetMethodArgumentsToLoadLength = (ushort)(targetMethodArgumentsToLoad?.Length ?? 0);
            TargetMinimumMajor = targetMinimumMajor;
            TargetMinimumMinor = targetMinimumMinor;
            TargetMinimumPatch = targetMinimumPatch;
            TargetMaximumMajor = targetMaximumMajor;
            TargetMaximumMinor = targetMaximumMinor;
            TargetMaximumPatch = targetMaximumPatch;
            WrapperAssembly = wrapperAssembly;
            WrapperType = wrapperType;
        }

        public void Dispose()
        {
            var ptr = TargetSignatureTypes;
            for (var i = 0; i < TargetSignatureTypesLength; i++)
            {
                Marshal.FreeHGlobal(Marshal.ReadIntPtr(ptr));
                ptr += Marshal.SizeOf(typeof(IntPtr));
            }

            Marshal.FreeHGlobal(TargetSignatureTypes);
            if (TargetMethodArgumentsToLoadLength > 0)
            {
                Marshal.FreeHGlobal(TargetMethodArgumentsToLoad);
            }
        }
    }
}
