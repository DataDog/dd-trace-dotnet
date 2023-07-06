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
        public readonly IntPtr TargetAssembly;

        public readonly IntPtr TargetType;

        public readonly IntPtr TargetMethod;

        public readonly IntPtr TargetSignatureTypes;

        public readonly ushort TargetSignatureTypesLength;

        public readonly ushort TargetMinimumMajor;

        public readonly ushort TargetMinimumMinor;

        public readonly ushort TargetMinimumPatch;

        public readonly ushort TargetMaximumMajor;

        public readonly ushort TargetMaximumMinor;

        public readonly ushort TargetMaximumPatch;

        public readonly IntPtr IntegrationAssembly;

        public readonly IntPtr IntegrationType;

        public NativeCallTargetDefinition(
            IntPtr targetAssembly,
            IntPtr targetType,
            IntPtr targetMethod,
            IntPtr targetSignatureTypes,
            ushort targetSignatureTypesLength,
            ushort targetMinimumMajor,
            ushort targetMinimumMinor,
            ushort targetMinimumPatch,
            ushort targetMaximumMajor,
            ushort targetMaximumMinor,
            ushort targetMaximumPatch,
            IntPtr integrationAssembly,
            IntPtr integrationType)
        {
            TargetAssembly = targetAssembly;
            TargetType = targetType;
            TargetMethod = targetMethod;
            TargetSignatureTypes = targetSignatureTypes;
            TargetSignatureTypesLength = targetSignatureTypesLength;
            TargetMinimumMajor = targetMinimumMajor;
            TargetMinimumMinor = targetMinimumMinor;
            TargetMinimumPatch = targetMinimumPatch;
            TargetMaximumMajor = targetMaximumMajor;
            TargetMaximumMinor = targetMaximumMinor;
            TargetMaximumPatch = targetMaximumPatch;
            IntegrationAssembly = integrationAssembly;
            IntegrationType = integrationType;
        }

        public NativeCallTargetDefinition(
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
            TargetAssembly = NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(targetAssembly);
            TargetType = NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(targetType);
            TargetMethod = NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(targetMethod);
            TargetSignatureTypes = NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray(targetSignatureTypes);
            TargetSignatureTypesLength = (ushort)(targetSignatureTypes?.Length ?? 0);
            TargetMinimumMajor = targetMinimumMajor;
            TargetMinimumMinor = targetMinimumMinor;
            TargetMinimumPatch = targetMinimumPatch;
            TargetMaximumMajor = targetMaximumMajor;
            TargetMaximumMinor = targetMaximumMinor;
            TargetMaximumPatch = targetMaximumPatch;
            IntegrationAssembly = NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(integrationAssembly);
            IntegrationType = NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(integrationType);
        }
    }
}
