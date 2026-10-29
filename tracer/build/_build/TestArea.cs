// <copyright file="TestArea.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace TestSelection;

/// <summary>
/// Test areas affected by a change. Values can be combined.
/// </summary>
[Flags]
public enum TestArea
{
    /// <summary>No test areas.</summary>
    None = 0,

    /// <summary>Tracing tests.</summary>
    Tracer = 1,

    /// <summary>Application security tests.</summary>
    Asm = 2,

    /// <summary>CI Visibility tests.</summary>
    CiVisibility = 4,

    /// <summary>Dynamic Instrumentation tests.</summary>
    Debugger = 8,

    /// <summary>Continuous Profiler tests.</summary>
    Profiler = 16,

    /// <summary>All test areas, used for shared or unclassified changes.</summary>
    All = Tracer | Asm | CiVisibility | Debugger | Profiler,
}
