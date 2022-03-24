// <copyright file="EnvironmentVariables.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal class EnvironmentVariables
    {
        public const string LibDdPprofPipepline = "DD_INTERNAL_PROFILING_LIBDDPROF_ENABLED";
        public const string ProfilingLogDir = "DD_PROFILING_LOG_DIR";
        public const string ProfilingPprofDir = "DD_PROFILING_OUTPUT_DIR";
        public const string ProfilerInstallationFolderEnvVar = "DD_TESTING_PROFILER_FOLDER";
    }
}
