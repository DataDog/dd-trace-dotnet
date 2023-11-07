// <copyright file="IClientConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// IClientConfig interface for ducktyping
    /// </summary>
    internal interface IClientConfig
    {
        /// <summary>
        /// Gets the region endpoint of the config
        /// </summary>
        IRegionEndpoint? RegionEndpoint { get; }
    }
}
