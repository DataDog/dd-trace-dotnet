// <copyright file="ErrorTag.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Represents a potential error returned by the trace exporter.
/// When `Tag` is `Some`, `Message` contains the error message.
/// </summary>
internal enum ErrorTag
{
    /// <summary>
    /// The error is present. `Message` contains the error message.
    /// </summary>
    Some,

    /// <summary>
    /// The error is not present.
    /// </summary>
    None,
}
