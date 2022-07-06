// <copyright file="EnvironmentVariables.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal class EnvironmentVariables
    {
        public const string ProfilingLogDir = "DD_PROFILING_LOG_DIR";
        public const string ProfilingPprofDir = "DD_INTERNAL_PROFILING_OUTPUT_DIR";
        public const string CodeHotSpotsEnable = "DD_PROFILING_CODEHOTSPOTS_ENABLED";
        public const string UseNativeLoader = "USE_NATIVE_LOADER";
        public const string CpuProfilerEnabled = "DD_PROFILING_CPU_ENABLED";
        public const string WallTimeProfilerEnabled = "DD_PROFILING_WALLTIME_ENABLED";
        public const string ExceptionProfilerEnabled = "DD_PROFILING_EXCEPTION_ENABLED";
        public const string ExceptionSampleLimit = "DD_PROFILING_EXCEPTION_SAMPLE_LIMIT";
    }
}
