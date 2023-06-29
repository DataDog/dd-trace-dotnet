// <copyright file="NativeCallTargetDefinition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.ClrProfiler
{
 // !                                         ██
 //                                         ██░░██
 //                                       ██░░░░░░██
 //                                     ██░░░░░░░░░░██
 //                                     ██░░░░░░░░░░██
 //                                   ██░░░░░░░░░░░░░░██
 //                                 ██░░░░░░██████░░░░░░██
 //                                 ██░░░░░░██████░░░░░░██
 //                               ██░░░░░░░░██████░░░░░░░░██
 //                               ██░░░░░░░░██████░░░░░░░░██
 //                             ██░░░░░░░░░░██████░░░░░░░░░░██
 //                           ██░░░░░░░░░░░░██████░░░░░░░░░░░░██
 //                           ██░░░░░░░░░░░░██████░░░░░░░░░░░░██
 //                         ██░░░░░░░░░░░░░░██████░░░░░░░░░░░░░░██
 //                         ██░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░██
 //                       ██░░░░░░░░░░░░░░░░██████░░░░░░░░░░░░░░░░██
 //                       ██░░░░░░░░░░░░░░░░██████░░░░░░░░░░░░░░░░██
 //                     ██░░░░░░░░░░░░░░░░░░██████░░░░░░░░░░░░░░░░░░██
 //                     ██░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░██
 //                       ██████████████████████████████████████████
 //
 // If you happen to change the layout of this structure,
 // this will lead to an AccessViolationException in netCore when using a more recent version of the nuget.
 // If you need to modify the definition, create a new interface NativeCallTargetDefinition# that will be consumed by the native layer

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NativeCallTargetDefinition
    {
        private static readonly int SizeOfPointer = Marshal.SizeOf(typeof(IntPtr));

        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetAssembly;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetType;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetMethod;

        public IntPtr TargetSignatureTypes;

        public ushort TargetSignatureTypesLength;

        public ushort TargetMinimumMajor;

        public ushort TargetMinimumMinor;

        public ushort TargetMinimumPatch;

        public ushort TargetMaximumMajor;

        public ushort TargetMaximumMinor;

        public ushort TargetMaximumPatch;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string IntegrationAssembly;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string IntegrationType;

        public unsafe NativeCallTargetDefinition(
                string targetAssembly,
                string targetType,
                string targetMethod,
                string[] targetSignatureTypes,
                ushort targetMinimumMajor,
                ushort targetMinimumMinor,
                ushort targetMinimumPatch,
                ushort targetMaximumMajor,
                ushort targetMaximumMinor,
                ushort targetMaximumPatch,
                string integrationAssembly,
                string integrationType)
        {
            TargetAssembly = targetAssembly;
            TargetType = targetType;
            TargetMethod = targetMethod;
            TargetSignatureTypes = IntPtr.Zero;
            if (targetSignatureTypes?.Length > 0)
            {
                TargetSignatureTypes = Marshal.AllocHGlobal(targetSignatureTypes.Length * SizeOfPointer);
                var stringPtrSize = 0;
                for (var i = 0; i < targetSignatureTypes.Length; i++)
                {
                    stringPtrSize += (targetSignatureTypes[i].Length * 2) + 1;
                }

                var targetSignatureTypesPointers = Marshal.AllocHGlobal(stringPtrSize);
                var stringPtr = targetSignatureTypesPointers;
                var ptr = TargetSignatureTypes;
                for (var i = 0; i < targetSignatureTypes.Length; i++)
                {
                    var str = targetSignatureTypes[i];
                    fixed (char* sPointer = str)
                    {
                        var writtenBytes = Encoding.Unicode.GetBytes(sPointer, str.Length, (byte*)stringPtr, str.Length * 2);
                        Marshal.WriteByte(stringPtr, writtenBytes, (byte)'\0');
                        Marshal.WriteIntPtr(ptr, stringPtr);
                        stringPtr = (IntPtr)((byte*)stringPtr + writtenBytes + 1);
                    }

                    ptr += SizeOfPointer;
                }
            }

            TargetSignatureTypesLength = (ushort)(targetSignatureTypes?.Length ?? 0);
            TargetMinimumMajor = targetMinimumMajor;
            TargetMinimumMinor = targetMinimumMinor;
            TargetMinimumPatch = targetMinimumPatch;
            TargetMaximumMajor = targetMaximumMajor;
            TargetMaximumMinor = targetMaximumMinor;
            TargetMaximumPatch = targetMaximumPatch;
            IntegrationAssembly = integrationAssembly;
            IntegrationType = integrationType;
        }

        public void Dispose()
        {
            if (TargetSignatureTypesLength > 0)
            {
                Marshal.FreeHGlobal(Marshal.ReadIntPtr(TargetSignatureTypes));
            }

            Marshal.FreeHGlobal(TargetSignatureTypes);
        }
    }
}
