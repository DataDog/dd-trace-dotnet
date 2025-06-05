// <copyright file="TraceExporterError.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Represents errors that can occur when exporting traces.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TraceExporterError
{
    /// <summary>
    /// The error code representing the domain of the error.
    /// Consumers can use this to determine how to handle the error.
    /// </summary>
    internal ErrorCode Code;

    /// <summary>
    /// Human-readable error message describing the error.
    /// </summary>
    internal IntPtr Msg;
}
