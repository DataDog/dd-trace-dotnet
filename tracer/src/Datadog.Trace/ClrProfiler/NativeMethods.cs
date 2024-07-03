// <copyright file="NativeMethods.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Iast.Analyzers;

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

        public static void RemoveCallTargetDefinitions(string id, NativeCallTargetDefinition[] methodArrays)
        {
            if (methodArrays is null || methodArrays.Length == 0)
            {
                return;
            }

            if (IsWindows)
            {
                Windows.RemoveCallTargetDefinitions(id, methodArrays, methodArrays.Length);
            }
            else
            {
                NonWindows.RemoveCallTargetDefinitions(id, methodArrays, methodArrays.Length);
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

        public static void DisableTracerCLRProfiler()
        {
            if (IsWindows)
            {
                Windows.DisableTracerCLRProfiler();
            }
            else
            {
                NonWindows.DisableTracerCLRProfiler();
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

        public static void AddInterfaceInstrumentations(string id, NativeCallTargetDefinition[] methodArrays)
        {
            if (methodArrays is null || methodArrays.Length == 0)
            {
                return;
            }

            if (IsWindows)
            {
                Windows.AddInterfaceInstrumentations(id, methodArrays, methodArrays.Length);
            }
            else
            {
                NonWindows.AddInterfaceInstrumentations(id, methodArrays, methodArrays.Length);
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

        public static int RegisterIastAspects(string[] aspects)
        {
            if (aspects == null || aspects.Length == 0)
            {
                return 0;
            }

            if (IsWindows)
            {
                return Windows.RegisterIastAspects(aspects, aspects.Length);
            }
            else
            {
                return NonWindows.RegisterIastAspects(aspects, aspects.Length);
            }
        }

        public static int RegisterCallTargetDefinitions(string id, NativeCallTargetDefinition2[] items, uint enabledCategories)
        {
            if (items == null || items.Length == 0)
            {
                return 0;
            }

            if (IsWindows)
            {
                return Windows.RegisterCallTargetDefinitions(id, items, items.Length, enabledCategories);
            }
            else
            {
                return NonWindows.RegisterCallTargetDefinitions(id, items, items.Length, enabledCategories);
            }
        }

        public static int EnableCallTargetDefinitions(uint enabledCategories)
        {
            if (enabledCategories == 0)
            {
                return 0;
            }

            if (IsWindows)
            {
                return Windows.EnableCallTargetDefinitions(enabledCategories);
            }
            else
            {
                return NonWindows.EnableCallTargetDefinitions(enabledCategories);
            }
        }

        public static int DisableCallTargetDefinitions(uint disabledCategories)
        {
            if (disabledCategories == 0)
            {
                return 0;
            }

            if (IsWindows)
            {
                return Windows.DisableCallTargetDefinitions(disabledCategories);
            }
            else
            {
                return NonWindows.DisableCallTargetDefinitions(disabledCategories);
            }
        }

        public static void UpdateSettings(string[] keys, string[] values)
        {
            if (keys.Length != values.Length)
            {
                throw new ArgumentException("keys and values must have the same length");
            }

            if (IsWindows)
            {
                Windows.UpdateSettings(keys, values, keys.Length);
            }
            else
            {
                NonWindows.UpdateSettings(keys, values, keys.Length);
            }
        }

        public static int GetUserStrings(int arrSize, [In, Out] UserStringInterop[] arr)
        {
            if (IsWindows)
            {
                return Windows.GetUserStrings(arrSize, arr);
            }
            else
            {
                return NonWindows.GetUserStrings(arrSize, arr);
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
            public static extern void RemoveCallTargetDefinitions([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition[] methodArrays, int size);

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void EnableByRefInstrumentation();

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void EnableCallTargetStateByRef();

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void AddDerivedInstrumentations([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition[] methodArrays, int size);

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void AddInterfaceInstrumentations([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition[] methodArrays, int size);

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void AddTraceAttributeInstrumentation([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string assemblyName, [MarshalAs(UnmanagedType.LPWStr)] string typeName);

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void InitializeTraceMethods([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string assemblyName, [MarshalAs(UnmanagedType.LPWStr)] string typeName, [MarshalAs(UnmanagedType.LPWStr)] string configuration);

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void DisableTracerCLRProfiler();

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern int RegisterIastAspects([In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 1)] string[] aspects, int aspectsLength);

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern int RegisterCallTargetDefinitions([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition2[] methodArrays, int size, uint enabledCategories);

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern int EnableCallTargetDefinitions(uint enabledCategories);

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern int DisableCallTargetDefinitions(uint disabledCategories);

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void UpdateSettings(
                [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] keys,
                [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] values,
                int length);

            [DllImport("Datadog.Tracer.Native.dll", CharSet = CharSet.Unicode)]
            public static extern int GetUserStrings(int arrSize, [In, Out] UserStringInterop[] arr);
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
            public static extern void RemoveCallTargetDefinitions([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition[] methodArrays, int size);

            [DllImport("Datadog.Tracer.Native")]
            public static extern void EnableByRefInstrumentation();

            [DllImport("Datadog.Tracer.Native")]
            public static extern void EnableCallTargetStateByRef();

            [DllImport("Datadog.Tracer.Native")]
            public static extern void AddDerivedInstrumentations([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition[] methodArrays, int size);

            [DllImport("Datadog.Tracer.Native")]
            public static extern void AddInterfaceInstrumentations([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition[] methodArrays, int size);

            [DllImport("Datadog.Tracer.Native")]
            public static extern void AddTraceAttributeInstrumentation([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string assemblyName, [MarshalAs(UnmanagedType.LPWStr)] string typeName);

            [DllImport("Datadog.Tracer.Native")]
            public static extern void InitializeTraceMethods([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string assemblyName, [MarshalAs(UnmanagedType.LPWStr)] string typeName, [MarshalAs(UnmanagedType.LPWStr)] string configuration);

            [DllImport("Datadog.Tracer.Native")]
            public static extern void DisableTracerCLRProfiler();

            [DllImport("Datadog.Tracer.Native")]
            public static extern int RegisterIastAspects([In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 1)] string[] aspects, int aspectsLength);

            [DllImport("Datadog.Tracer.Native")]
            public static extern int RegisterCallTargetDefinitions([MarshalAs(UnmanagedType.LPWStr)] string id, [In] NativeCallTargetDefinition2[] methodArrays, int size, uint enabledCategories);

            [DllImport("Datadog.Tracer.Native")]
            public static extern int EnableCallTargetDefinitions(uint enabledCategories);

            [DllImport("Datadog.Tracer.Native")]
            public static extern int DisableCallTargetDefinitions(uint disabledCategories);

            [DllImport("Datadog.Tracer.Native")]
            public static extern void UpdateSettings(
                [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] keys,
                [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] values,
                int length);

            [DllImport("Datadog.Tracer.Native", CharSet = CharSet.Unicode)]
            public static extern int GetUserStrings(int arrSize, [In, Out] UserStringInterop[] arr);
        }
    }
}
