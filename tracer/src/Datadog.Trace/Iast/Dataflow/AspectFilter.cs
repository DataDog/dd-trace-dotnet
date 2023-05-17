// <copyright file="AspectFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Iast.Dataflow;

/// <summary>
/// Available Aspect Filters
/// </summary>
internal enum AspectFilter
{
    /// <summary> No filter </summary>
    None,

    /// <summary> Common string optimizations </summary>
    StringOptimization,

    /// <summary> Filter if all params are String Literals </summary>
    StringLiterals,

    /// <summary> Filter if any of the params are String Literals </summary>
    StringLiterals_Any,

    /// <summary> Filter if param0 is String Literal </summary>
    StringLiteral_0,

    /// <summary> Filter if param1 is String Literal </summary>
    StringLiteral_1,
}
