// <copyright file="EnvironmentVariables.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ContinuousProfiler
{
    internal static class EnvironmentVariables
    {
        public const string ProfilingEnabled = "DD_PROFILING_ENABLED";
        public const string CodeHotspotEnabled = "DD_PROFILING_CODEHOTSPOT_ENABLED";
    }
}
