// <copyright file="MockTracerResponseType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.TestHelpers;

public enum MockTracerResponseType
{
    /// <summary>
    /// Any request which doesn't match a known endpoint
    /// </summary>
    Unknown,

    /// <summary>
    /// The trace endpoint
    /// </summary>
    Traces,

    /// <summary>
    /// The Telemetry endpoint
    /// </summary>
    Telemetry,

    /// <summary>
    /// The discovery endpoint
    /// </summary>
    Info,

    /// <summary>
    /// The dynamic configuration endpoint
    /// </summary>
    Debugger,

    /// <summary>
    /// The trace stats endpoint
    /// </summary>
    Stats,

    /// <summary>
    /// The remote configuration endpoint
    /// </summary>
    RemoteConfig,

    /// <summary>
    /// The Data streams endpoint
    /// </summary>
    DataStreams,

    /// <summary>
    /// The CI Visibility EVP proxy endpoint
    /// </summary>
    EvpProxy,

    /// <summary>
    /// The Tracer flare endpoint
    /// </summary>
    TracerFlare,
}
