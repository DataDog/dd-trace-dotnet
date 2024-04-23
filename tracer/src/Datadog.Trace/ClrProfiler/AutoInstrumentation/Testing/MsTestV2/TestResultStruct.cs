// <copyright file="TestResultStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.DuckTyping;

#pragma warning disable CS0649 // Field is never assigned to

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// TestResult ducktype struct
/// </summary>
[DuckCopy]
internal struct TestResultStruct
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
    /// Test failure exception
    /// </summary>
    public Exception? TestFailureException;
}
