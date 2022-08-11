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
        private static readonly bool IsWindows = FrameworkDescription.Instance.IsWindows();

        public static bool IsProfilerAttached()
        {
            if (IsWindows)
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

            if (IsWindows)
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
            if (IsWindows)
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
            if (IsWindows)
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

            if (IsWindows)
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

            if (IsWindows)
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

            if (IsWindows)
            {
                Windows.InitializeTraceMethods(id, assemblyName, typeName, configuration);
            }
            else
            {
                NonWindows.InitializeTraceMethods(id, assemblyName, typeName, configuration);
            }
        }

        // the "dll" extension is required on .NET Framework
        // and optional on .NET Core
        // The DllImport methods are re-written by cor_profiler to have the correct vales
        private static class Windows
        {
            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern bool IsProfilerAttached();

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void InitializeProfiler([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition[] methodArrays, int size);

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void EnableByRefInstrumentation();

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void EnableCallTargetStateByRef();

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void AddDerivedInstrumentations([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition[] methodArrays, int size);

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void AddTraceAttributeInstrumentation([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string assemblyName, [MarshalAs(UnmanagedType.LPWStr)] string typeName);

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void InitializeTraceMethods([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string assemblyName, [MarshalAs(UnmanagedType.LPWStr)] string typeName, [MarshalAs(UnmanagedType.LPWStr)] string configuration);
        }

        // assume .NET Core if not running on Windows
        // The DllImport methods are re-written by cor_profiler to have the correct vales
        private static class NonWindows
        {
            [DllImport("Datadog.Tracer.Native")]
            public static extern bool IsProfilerAttached();

            [DllImport("Datadog.Tracer.Native")]
            public static extern void InitializeProfiler([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition[] methodArrays, int size);

            [DllImport("Datadog.Tracer.Native")]
            public static extern void EnableByRefInstrumentation();

            [DllImport("Datadog.Tracer.Native")]
            public static extern void EnableCallTargetStateByRef();

            [DllImport("Datadog.Tracer.Native")]
            public static extern void AddDerivedInstrumentations([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition[] methodArrays, int size);

            [DllImport("Datadog.Tracer.Native")]
            public static extern void AddTraceAttributeInstrumentation([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string assemblyName, [MarshalAs(UnmanagedType.LPWStr)] string typeName);

            [DllImport("Datadog.Tracer.Native")]
            public static extern void InitializeTraceMethods([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string assemblyName, [MarshalAs(UnmanagedType.LPWStr)] string typeName, [MarshalAs(UnmanagedType.LPWStr)] string configuration);
        }
    }
}
