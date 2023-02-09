// <copyright file="BenchmarkTestTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.Tags;

/// <summary>
/// Span tags for benchmark test data model
/// </summary>
internal static class BenchmarkTestTags
{
    /// <summary>
    /// Host processor name
    /// </summary>
    public const string HostProcessorName = "host.processor.name";

    /// <summary>
    /// Host processor physical processor count
    /// </summary>
    public const string HostProcessorPhysicalProcessorCount = "host.processor.physical_processor_count";

    /// <summary>
    /// Host processor physical core count
    /// </summary>
    public const string HostProcessorPhysicalCoreCount = "host.processor.physical_core_count";

    /// <summary>
    /// Host processor logical core count
    /// </summary>
    public const string HostProcessorLogicalCoreCount = "host.processor.logical_core_count";

    /// <summary>
    /// Host processor max frequency hertz
    /// </summary>
    public const string HostProcessorMaxFrequencyHertz = "host.processor.max_frequency_hertz";

    /// <summary>
    /// Host os version
    /// </summary>
    public const string HostOsVersion = "host.os_version";

    /// <summary>
    /// Host runtime version
    /// </summary>
    public const string HostRuntimeVersion = "host.runtime_version";

    /// <summary>
    /// Host chronometer frequency hertz
    /// </summary>
    public const string HostChronometerFrequencyHertz = "host.chronometer_frequency_hertz";

    /// <summary>
    /// Host chronometer resolution
    /// </summary>
    public const string HostChronometerResolution = "host.chronometer_resolution";

    /// <summary>
    /// Job description
    /// </summary>
    public const string JobDescription = "test.configuration.job_description";

    /// <summary>
    /// Job platform
    /// </summary>
    public const string JobPlatform = "test.configuration.job_platform";

    /// <summary>
    /// Job runtime name
    /// </summary>
    public const string JobRuntimeName = "test.configuration.job_runtime_name";

    /// <summary>
    /// Job runtime name
    /// </summary>
    public const string JobRuntimeMoniker = "test.configuration.job_runtime_moniker";
}
