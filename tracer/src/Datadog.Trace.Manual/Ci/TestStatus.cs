// <copyright file="TestStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Ci;

/// <summary>
/// Test status
/// </summary>
public enum TestStatus
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
