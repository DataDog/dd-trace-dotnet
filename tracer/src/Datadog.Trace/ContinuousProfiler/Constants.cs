// <copyright file="Constants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ContinuousProfiler
{
    internal class Constants
    {
#if NETFRAMEWORK
        internal const string NativeProfilerLibNameX86 = "Datadog.AutoInstrumentation.Profiler.Native.x86.dll";
        internal const string NativeProfilerLibNameX64 = "Datadog.AutoInstrumentation.Profiler.Native.x64.dll";
#else
        internal const string NativeProfilerLibNameX86 = "Datadog.AutoInstrumentation.Profiler.Native.x86";
        internal const string NativeProfilerLibNameX64 = "Datadog.AutoInstrumentation.Profiler.Native.x64";
#endif
    }
}
