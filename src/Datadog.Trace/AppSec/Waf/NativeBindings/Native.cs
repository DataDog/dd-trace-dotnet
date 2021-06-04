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

        [DllImport(DllName)]
        internal static extern PWRet pw_runH(IntPtr wafHandle, PWArgs64 parameters, ulong timeLeftInUs);

        [DllImport(DllName)]
        internal static extern void pw_freeReturn(PWRet output);

        [DllImport(DllName)]
        internal static extern IntPtr pw_initAdditiveH(IntPtr powerwafHandle);

        [DllImport(DllName)]
        internal static extern PWRet pw_runAdditive(IntPtr context, PWArgs64 newArgs, ulong timeLeftInUs);

        [DllImport(DllName)]
        internal static extern void pw_clearAdditive(IntPtr context);

        [DllImport(DllName)]
        internal static extern PWArgs64 pw_getInvalid();

        [DllImport(DllName)]
        internal static extern PWArgs64 pw_createStringWithLength(string s, ulong length);

        [DllImport(DllName)]
        internal static extern PWArgs64 pw_createString(string s);

        [DllImport(DllName)]
        internal static extern PWArgs64 pw_createInt(long value);

        [DllImport(DllName)]
        internal static extern PWArgs64 pw_createUint(ulong value);

        [DllImport(DllName)]
        internal static extern PWArgs64 pw_createArray();

        [DllImport(DllName)]
        internal static extern PWArgs64 pw_createMap();

        [DllImport(DllName)]
        internal static extern bool pw_addArray(ref PWArgs64 array, PWArgs64 entry);

        // Setting entryNameLength to 0 will result in the entryName length being re-computed with strlen
        [DllImport(DllName)]
        internal static extern bool pw_addMap(ref PWArgs64 map, string entryName, ulong entryNameLength, PWArgs64 entry);

        [DllImport(DllName)]
        internal static extern void pw_freeArg(ref PWArgs64 input);

#pragma warning restore SA1300 // Element should begin with upper-case letter
    }
}
