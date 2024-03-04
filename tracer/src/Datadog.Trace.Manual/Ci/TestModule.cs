// <copyright file="TestModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Ci.Stubs;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test module
/// </summary>
public static class TestModule
{
    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <returns>New test module instance</returns>
    public static ITestModule Create(string name)
        => InternalCreate(name, null, null, null);

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <returns>New test module instance</returns>
    public static ITestModule Create(string name, string framework, string frameworkVersion)
        => InternalCreate(name, framework, frameworkVersion, null);

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <param name="startDate">Test session start date</param>
    /// <returns>New test module instance</returns>
    public static ITestModule Create(string name, string framework, string frameworkVersion, DateTimeOffset startDate)
        => InternalCreate(name, framework, frameworkVersion, startDate);

    [Instrumented]
    internal static ITestModule InternalCreate(string name, string? framework, string? frameworkVersion, DateTimeOffset? startDate)
        => NullTestModule.Instance;
}
