// <copyright file="ITestResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal interface ITestResult : IDuckType
{
    /// <summary>
    /// Gets or sets the outcome enum
    /// </summary>
    public UnitTestOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the test failure exception
    /// </summary>
    public Exception? TestFailureException { get; set; }

    /// <summary>
    /// Gets or sets the inner results count of the result.
    /// </summary>
    public int InnerResultsCount { get; set; }

    /// <summary>
    /// Gets or sets the duration of test execution.
    /// </summary>
    public TimeSpan Duration { get; set; }
}
