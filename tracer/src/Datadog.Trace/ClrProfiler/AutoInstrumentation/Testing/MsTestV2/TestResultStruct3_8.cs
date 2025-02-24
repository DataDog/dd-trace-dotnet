// <copyright file="TestResultStruct3_8.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// TestResult ducktype struct
/// </summary>
[DuckCopy]
// ReSharper disable once InconsistentNaming
internal struct TestResultStruct3_8
{
    /// <summary>
    /// Gets the outcome enum
    /// </summary>
    public UnitTestOutcome Outcome;

    /// <summary>
    /// Gets the display name
    /// </summary>
    public string? DisplayName;

    /// <summary>
    /// Gets the ignore reason
    /// </summary>
    public string? IgnoreReason;
}
