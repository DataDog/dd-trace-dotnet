// <copyright file="IastMetricsVerbosityLevel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;

namespace Datadog.Trace.Iast.Telemetry;

/// <summary>
/// The verbosity telemetry level to be used by Iast
/// </summary>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
internal enum IastMetricsVerbosityLevel
{
    /// <summary>
    /// Disabled
    /// </summary>
    Off = 0,

    /// <summary>
    /// Only mandatory metrics
    /// </summary>
    Mandatory = 1,

    /// <summary>
    /// The default log level
    /// </summary>
    Information = 2,

    /// <summary>
    /// Debug
    /// </summary>
    Debug = 3,
}
