// <copyright file="ProfilerState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ContinuousProfiler;

internal enum ProfilerState
{
    /// <summary>
    /// The profiler is explicitly disabled via configuration
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// The profiler is explicitly enabled via configuration
    /// </summary>
    Enabled = 1,

    /// <summary>
    /// The profiler is in "auto" mode; i.e. will start after a delay and if traces are created
    /// </summary>
    Auto = 2,
}
