// <copyright file="MaybeError.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Represents a potential error returned by the trace exporter.
/// `Tag` indicates whether the error is present or not.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MaybeError
{
    /// <summary>
    /// The tag indicating whether the error is present or not.
    /// </summary>
    internal ErrorTag Tag;

    /// <summary>
    /// The message associated with the error.
    /// </summary>
    internal CharSlice Message;
}
