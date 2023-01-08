// <copyright file="EvaluateAt.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.Configurations.Models;

/// <summary>
/// Method phase
/// </summary>
internal enum EvaluateAt
{
    /// <summary>
    /// Entry of the method
    /// </summary>
    Entry,

    /// <summary>
    /// Exit of the method
    /// </summary>
    Exit
}
