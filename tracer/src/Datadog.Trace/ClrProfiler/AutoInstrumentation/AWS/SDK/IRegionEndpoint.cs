// <copyright file="IRegionEndpoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// IRegionEndpoint interface for ducktyping
    /// </summary>
    internal interface IRegionEndpoint
    {
        /// <summary>
        /// Gets the system name of the region endpoint
        /// </summary>
        string SystemName { get; }
    }
}
