// <copyright file="ProbeMode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger;

internal enum ProbeMode
{
    /// <summary>
    /// Read the probe configuration from datadog-agent via Remote Configuration Management.
    /// </summary>
    Agent,

    /// <summary>
    /// Read the probe configuration from a local file on disk. Useful for local development and testing scenarios.
    /// </summary>
    File
}
