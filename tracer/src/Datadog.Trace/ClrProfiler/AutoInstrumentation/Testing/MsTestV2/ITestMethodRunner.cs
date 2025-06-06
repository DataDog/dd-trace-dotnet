// <copyright file="ITestMethodRunner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// TestMethodRunner ducktype interface
/// </summary>
internal interface ITestMethodRunner
{
    /// <summary>
    /// Gets the TestMethodInfo instance
    /// </summary>
    [DuckField(Name = "testMethodInfo,_testMethodInfo")]
    ITestMethodInfo? TestMethodInfo { get; }
}

/// <summary>
/// TestMethodRunner (v3_9) ducktype interface
/// </summary>
internal interface ITestMethodRunnerV3_9
{
    /// <summary>
    /// Gets the TestMethodInfo instance
    /// </summary>
    [DuckField(Name = "testMethodInfo,_testMethodInfo")]
    ITestMethodInfoV3_9? TestMethodInfo { get; }
}
