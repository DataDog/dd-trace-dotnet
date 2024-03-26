// <copyright file="NativeSpanProbeDefinition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Debugger.PInvoke
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NativeSpanProbeDefinition : IDisposable
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string ProbeId;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetType;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetMethod;

        public IntPtr TargetParameterTypes;

        public ushort TargetSignatureTypesLength;

        public NativeSpanProbeDefinition(
                string probeId,
                string targetTypeFullName,
                string targetMethodName,
                string[] targetParameterTypesFullName)
        {
            ProbeId = probeId;
            TargetType = targetTypeFullName;
            TargetMethod = targetMethodName;
            TargetParameterTypes = IntPtr.Zero;
            if (targetParameterTypesFullName?.Length > 0)
            {
                TargetParameterTypes = Marshal.AllocHGlobal(targetParameterTypesFullName.Length * Marshal.SizeOf(typeof(IntPtr)));
                var ptr = TargetParameterTypes;
                for (var i = 0; i < targetParameterTypesFullName.Length; i++)
                {
                    Marshal.WriteIntPtr(ptr, Marshal.StringToHGlobalUni(targetParameterTypesFullName[i]));
                    ptr += Marshal.SizeOf(typeof(IntPtr));
                }
            }

            TargetSignatureTypesLength = (ushort)(targetParameterTypesFullName?.Length ?? 0);
        }

        public void Dispose()
        {
            var ptr = TargetParameterTypes;
            for (var i = 0; i < TargetSignatureTypesLength; i++)
            {
                Marshal.FreeHGlobal(Marshal.ReadIntPtr(ptr));
                ptr += Marshal.SizeOf(typeof(IntPtr));
            }

            Marshal.FreeHGlobal(TargetParameterTypes);
        }
    }
}
