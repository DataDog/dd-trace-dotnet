// <copyright file="AspectType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Iast;

/// <summary>
/// Type of an aspect function
/// </summary>
internal enum AspectType
{
    /// <summary> Default / undefined </summary>
    DEFAULT,

    /// <summary> Propagation aspect </summary>
    PROPAGATION,

    /// <summary> Sink aspect </summary>
    SINK,

    /// <summary> Source aspect </summary>
    SOURCE,
}
