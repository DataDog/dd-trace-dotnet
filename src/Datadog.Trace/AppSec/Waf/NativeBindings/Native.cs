// <copyright file="Native.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#endif
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal class Native
    {
#if !NETFRAMEWORK
        private const string DllName = "Datadog.Trace.ClrProfiler.Native";
#else
        private const string DllName = "Datadog.Trace.ClrProfiler.Native.dll";
#endif
#pragma warning disable SA1300 // Element should begin with upper-case letter

        [DllImport(DllName)]
        internal static extern PWVersion pw_getVersion();

        [DllImport(DllName)]
        internal static extern IntPtr pw_initH(string wafRule, ref PWConfig config, ref string errors);

        [DllImport(DllName)]
        internal static extern void pw_clearRuleH(IntPtr wafHandle);

        [DllImport("Datadog.Trace.ClrProfiler.Native")]
        internal static extern PWRet pw_runH(IntPtr wafHandle, PWArgs parameters, ulong timeLeftInUs);

        [DllImport(DllName)]
        internal static extern void pw_freeReturn(PWRet output);

        [DllImport(DllName)]
        internal static extern IntPtr pw_initAdditiveH(IntPtr powerwafHandle);

        [DllImport("Datadog.Trace.ClrProfiler.Native")]
        internal static extern PWRet pw_runAdditive(IntPtr context, PWArgs newArgs, ulong timeLeftInUs);

        [DllImport(DllName)]
        internal static extern void pw_clearAdditive(IntPtr context);

        [DllImport("Datadog.Trace.ClrProfiler.Native")]
        internal static extern PWArgs pw_getInvalid();

        [DllImport("Datadog.Trace.ClrProfiler.Native")]
        internal static extern PWArgs pw_createStringWithLength(string s, ulong length);

        [DllImport("Datadog.Trace.ClrProfiler.Native")]
        internal static extern PWArgs pw_createString(string s);

        [DllImport("Datadog.Trace.ClrProfiler.Native")]
        internal static extern PWArgs pw_createInt(long value);

        [DllImport("Datadog.Trace.ClrProfiler.Native")]
        internal static extern PWArgs pw_createUint(ulong value);

        [DllImport("Datadog.Trace.ClrProfiler.Native")]
        internal static extern PWArgs pw_createArray();

        [DllImport("Datadog.Trace.ClrProfiler.Native")]
        internal static extern PWArgs pw_createMap();

        [DllImport("Datadog.Trace.ClrProfiler.Native")]
        internal static extern bool pw_addArray(ref PWArgs array, PWArgs entry);

        // Setting entryNameLength to 0 will result in the entryName length being re-computed with strlen
        [DllImport("Datadog.Trace.ClrProfiler.Native")]
        internal static extern bool pw_addMap(ref PWArgs map, string entryName, ulong entryNameLength, PWArgs entry);

        [DllImport("Datadog.Trace.ClrProfiler.Native")]
        internal static extern void pw_freeArg(ref PWArgs input);

#pragma warning restore SA1300 // Element should begin with upper-case letter
    }
}
