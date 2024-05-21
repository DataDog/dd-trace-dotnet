// <copyright file="AspectType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Iast.Dataflow;

/// <summary>
/// Type of an aspect function
/// </summary>
internal enum AspectType
{
    /// <summary> Default / undefined </summary>
    Default,

    /// <summary> Propagation aspect </summary>
    Propagation,

    /// <summary> Sink aspect </summary>
    Sink,

    /// <summary> Source aspect </summary>
    Source,

    /// <summary> Rasp and Iast Sink aspect </summary>
    RaspIastSink,
}
