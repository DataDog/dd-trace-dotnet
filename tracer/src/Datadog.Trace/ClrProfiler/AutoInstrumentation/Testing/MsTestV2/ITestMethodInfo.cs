// <copyright file="ITestMethodInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// TestMethodInfo ducktype interface
/// </summary>
internal interface ITestMethodInfo : ITestMethodInfoWithParent
{
    /// <summary>
    /// Gets the test method options
    /// </summary>
    ITestMethodOptions? TestMethodOptions { get; }
}

/// <summary>
/// TestMethodInfo (v3_9) ducktype interface
/// </summary>
internal interface ITestMethodInfoV3_9 : ITestMethodInfoWithParent
{
    /// <summary>
    /// Gets or sets the test executor.
    /// </summary>
    object? Executor { get; set; }
}

internal interface ITestMethodInfoWithParent : ITestMethod
{
    /// <summary>
    /// Gets the parent class Info object.
    /// </summary>
    ITestClassInfo? Parent { get; }
}
