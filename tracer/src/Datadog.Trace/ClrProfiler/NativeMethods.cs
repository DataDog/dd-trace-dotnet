// <copyright file="NativeMethods.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;

// ReSharper disable MemberHidesStaticFromOuterClass
namespace Datadog.Trace.ClrProfiler
{
    internal static class NativeMethods
    {
        private static readonly Lazy<bool> IsWindows = new(() => FrameworkDescription.Instance.IsWindows());

        public static bool IsProfilerAttached()
        {
            if (IsWindows.Value)
            {
                return Windows.IsProfilerAttached();
            }

            return NonWindows.IsProfilerAttached();
        }

        public static void InitializeProfiler(string id, NativeCallTargetDefinition[] methodArrays)
        {
            if (methodArrays is null || methodArrays.Length == 0)
            {
                return;
            }

            if (IsWindows.Value)
            {
                Windows.InitializeProfiler(id, methodArrays, methodArrays.Length);
            }
            else
            {
                NonWindows.InitializeProfiler(id, methodArrays, methodArrays.Length);
            }
        }

        public static void EnableByRefInstrumentation()
        {
            if (IsWindows.Value)
            {
                Windows.EnableByRefInstrumentation();
            }
            else
            {
                NonWindows.EnableByRefInstrumentation();
            }
        }

        public static void EnableCallTargetStateByRef()
        {
            if (IsWindows.Value)
            {
                Windows.EnableCallTargetStateByRef();
            }
            else
            {
                NonWindows.EnableCallTargetStateByRef();
            }
        }

        public static void AddDerivedInstrumentations(string id, NativeCallTargetDefinition[] methodArrays)
        {
            if (methodArrays is null || methodArrays.Length == 0)
            {
                return;
            }

            if (IsWindows.Value)
            {
                Windows.AddDerivedInstrumentations(id, methodArrays, methodArrays.Length);
            }
            else
            {
                NonWindows.AddDerivedInstrumentations(id, methodArrays, methodArrays.Length);
            }
        }

        public static void AddTraceAttributeInstrumentation(string id, string assemblyName, string typeName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName)
                || string.IsNullOrWhiteSpace(typeName))
            {
                return;
            }

            if (IsWindows.Value)
            {
                Windows.AddTraceAttributeInstrumentation(id, assemblyName, typeName);
            }
            else
            {
                NonWindows.AddTraceAttributeInstrumentation(id, assemblyName, typeName);
            }
        }

        public static void InitializeTraceMethods(string id, string assemblyName, string typeName, string configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration)
                || string.IsNullOrWhiteSpace(assemblyName)
                || string.IsNullOrWhiteSpace(typeName))
            {
                return;
            }

            if (IsWindows.Value)
            {
                Windows.InitializeTraceMethods(id, assemblyName, typeName, configuration);
            }
            else
            {
                NonWindows.InitializeTraceMethods(id, assemblyName, typeName, configuration);
            }
        }

        public static void Initialize(
            string id,
            NativeCallTargetDefinition[] definitionsArray,
            NativeCallTargetDefinition[] derivedDefinitionsArray,
            string traceAttributeAssemblyName,
            string traceAttributeTypeName,
            string traceMethodAssemblyName,
            string traceMethodTypeName,
            string traceMethodConfiguration)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            definitionsArray ??= Array.Empty<NativeCallTargetDefinition>();
            derivedDefinitionsArray ??= Array.Empty<NativeCallTargetDefinition>();
            traceAttributeAssemblyName ??= string.Empty;
            traceAttributeTypeName ??= string.Empty;
            traceMethodAssemblyName ??= string.Empty;
            traceMethodTypeName ??= string.Empty;
            traceMethodConfiguration ??= string.Empty;

            Interop.Initialize(
                id,
                definitionsArray,
                definitionsArray.Length,
                derivedDefinitionsArray,
                derivedDefinitionsArray.Length,
                traceAttributeAssemblyName,
                traceAttributeTypeName,
                traceMethodAssemblyName,
                traceMethodTypeName,
                traceMethodConfiguration);
        }

        // the "dll" extension is required on .NET Framework
        // and optional on .NET Core
        private static class Windows
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern bool IsProfilerAttached();

            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern void InitializeProfiler([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition[] methodArrays, int size);

            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern void EnableByRefInstrumentation();

            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern void EnableCallTargetStateByRef();

            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern void AddDerivedInstrumentations([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition[] methodArrays, int size);

            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern void AddTraceAttributeInstrumentation([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string assemblyName, [MarshalAs(UnmanagedType.LPWStr)] string typeName);

            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern void InitializeTraceMethods([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string assemblyName, [MarshalAs(UnmanagedType.LPWStr)] string typeName, [MarshalAs(UnmanagedType.LPWStr)] string configuration);
        }

        // assume .NET Core if not running on Windows
        private static class NonWindows
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native")]
            public static extern bool IsProfilerAttached();

            [DllImport("Datadog.Trace.ClrProfiler.Native")]
            public static extern void InitializeProfiler([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition[] methodArrays, int size);

            [DllImport("Datadog.Trace.ClrProfiler.Native")]
            public static extern void EnableByRefInstrumentation();

            [DllImport("Datadog.Trace.ClrProfiler.Native")]
            public static extern void EnableCallTargetStateByRef();

            [DllImport("Datadog.Trace.ClrProfiler.Native")]
            public static extern void AddDerivedInstrumentations([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition[] methodArrays, int size);

            [DllImport("Datadog.Trace.ClrProfiler.Native")]
            public static extern void AddTraceAttributeInstrumentation([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string assemblyName, [MarshalAs(UnmanagedType.LPWStr)] string typeName);

            [DllImport("Datadog.Trace.ClrProfiler.Native")]
            public static extern void InitializeTraceMethods([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string assemblyName, [MarshalAs(UnmanagedType.LPWStr)] string typeName, [MarshalAs(UnmanagedType.LPWStr)] string configuration);
        }

        // Because we are rewriting the PInvoke maps we don't need to have a Windows and NonWindows implementation
        // We kept these classes for backward compatibility (we can remove it in a next major version)
        private static class Interop
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern void Initialize(
                [MarshalAs(UnmanagedType.LPWStr)] string id,
                [In] NativeCallTargetDefinition[] definitionsArray,
                int definitionsSize,
                [In] NativeCallTargetDefinition[] derivedDefinitionsArray,
                int derivedDefinitionsSize,
                [MarshalAs(UnmanagedType.LPWStr)] string traceAttributeAssemblyName,
                [MarshalAs(UnmanagedType.LPWStr)] string traceAttributeTypeName,
                [MarshalAs(UnmanagedType.LPWStr)] string traceMethodAssemblyName,
                [MarshalAs(UnmanagedType.LPWStr)] string traceMethodTypeName,
                [MarshalAs(UnmanagedType.LPWStr)] string traceMethodConfiguration);
        }
    }
}
