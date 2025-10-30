// <copyright file="ConfigurationKeys.Profiler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration
{
    internal static partial class ConfigurationKeys
    {
        internal static class Profiler
        {
            public const string ProfilingEnabled = "DD_PROFILING_ENABLED";
            public const string CodeHotspotsEnabled = "DD_PROFILING_CODEHOTSPOTS_ENABLED";
            public const string EndpointProfilingEnabled = "DD_PROFILING_ENDPOINT_COLLECTION_ENABLED";
            public const string SsiDeployed = "DD_INJECTION_ENABLED";
            public const string ProfilerManagedActivationEnabled = "DD_PROFILING_MANAGED_ACTIVATION_ENABLED";
        }
    }
}
