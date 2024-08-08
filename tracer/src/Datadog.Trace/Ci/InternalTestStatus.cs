// <copyright file="InternalTestStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Internal.Ci;

/// <summary>
/// Test status
/// </summary>
internal enum InternalTestStatus
{
    /// <summary>
    /// Pass test status
    /// </summary>
    Pass,

    /// <summary>
    /// Fail test status
    /// </summary>
    Fail,

    /// <summary>
    /// Skip test status
    /// </summary>
    Skip
}
