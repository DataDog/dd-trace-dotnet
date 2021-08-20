// <copyright file="NativeCallTargetDefinition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

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
            TargetMinimumMajor = targetMinimumMajor;
            TargetMinimumMinor = targetMinimumMinor;
            TargetMinimumPatch = targetMinimumPatch;
            TargetMaximumMajor = targetMaximumMajor;
            TargetMaximumMinor = targetMaximumMinor;
            TargetMaximumPatch = targetMaximumPatch;
            WrapperAssembly = wrapperAssembly;
            WrapperType = wrapperType;
        }

        public static NativeCallTargetDefinition[] GetAllDefinitions()
        {
            var currentAssembly = typeof(NativeCallTargetDefinition).Assembly;

            var lstDefinitions = new List<NativeCallTargetDefinition>(250);

            foreach (var attr in currentAssembly.GetCustomAttributes(inherit: false))
            {
                if (attr is InstrumentMethodAttribute instMethodAttr)
                {
                    ProcessAttribute(instMethodAttr, lstDefinitions);
                }
            }

            Type[] assemblyTypes;

            try
            {
                assemblyTypes = currentAssembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                assemblyTypes = e.Types;
            }

            foreach (var type in assemblyTypes)
            {
                if (type is null)
                {
                    continue;
                }

                foreach (var attr in type.GetCustomAttributes(inherit: false))
                {
                    if (attr is InstrumentMethodAttribute instMethodAttr)
                    {
                        instMethodAttr.CallTargetType = type;
                        ProcessAttribute(instMethodAttr, lstDefinitions);
                    }
                }
            }

            return lstDefinitions.ToArray();

            static void ProcessAttribute(InstrumentMethodAttribute instMethodAttr, List<NativeCallTargetDefinition> lstDefinitions)
            {
                foreach (string assemblyName in instMethodAttr.AssemblyNames)
                {
                    lstDefinitions.Add(new NativeCallTargetDefinition(
                        assemblyName,
                        instMethodAttr.TypeName,
                        instMethodAttr.MethodName,
                        GetSignatureTypes(instMethodAttr),
                        instMethodAttr.VersionRange.MinimumMajor,
                        instMethodAttr.VersionRange.MinimumMinor,
                        instMethodAttr.VersionRange.MinimumPatch,
                        instMethodAttr.VersionRange.MaximumMajor,
                        instMethodAttr.VersionRange.MaximumMinor,
                        instMethodAttr.VersionRange.MaximumPatch,
                        instMethodAttr.CallTargetType.Assembly.FullName,
                        instMethodAttr.CallTargetType.FullName));
                }
            }

            static string[] GetSignatureTypes(InstrumentMethodAttribute instMethodAttr)
            {
                var retSignature = new string[(instMethodAttr?.ParameterTypeNames?.Length ?? 0) + 1];
                retSignature[0] = instMethodAttr.ReturnTypeName;

                if (instMethodAttr.ParameterTypeNames is not null)
                {
                    for (var i = 0; i < instMethodAttr.ParameterTypeNames.Length; i++)
                    {
                        retSignature[i + 1] = instMethodAttr.ParameterTypeNames[i];
                    }
                }

                return retSignature;
            }
        }
    }
}
