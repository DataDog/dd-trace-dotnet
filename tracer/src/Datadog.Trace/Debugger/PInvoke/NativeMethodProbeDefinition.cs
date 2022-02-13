// <copyright file="NativeMethodProbeDefinition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Debugger.PInvoke
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NativeMethodProbeDefinition
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetAssembly;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetType;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetMethod;

        public IntPtr TargetParameterTypes;

        public ushort TargetSignatureTypesLength;

        public NativeMethodProbeDefinition(
                string targetAssembly,
                string targetType,
                string targetMethod,
                string[] targetParameterTypes)
        {
            TargetAssembly = targetAssembly;
            TargetType = targetType;
            TargetMethod = targetMethod;
            TargetParameterTypes = IntPtr.Zero;
            if (targetParameterTypes?.Length > 0)
            {
                TargetParameterTypes = Marshal.AllocHGlobal(targetParameterTypes.Length * Marshal.SizeOf(typeof(IntPtr)));
                var ptr = TargetParameterTypes;
                for (var i = 0; i < targetParameterTypes.Length; i++)
                {
                    Marshal.WriteIntPtr(ptr, Marshal.StringToHGlobalUni(targetParameterTypes[i]));
                    ptr += Marshal.SizeOf(typeof(IntPtr));
                }
            }

            TargetSignatureTypesLength = (ushort)(targetParameterTypes?.Length ?? 0);
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
