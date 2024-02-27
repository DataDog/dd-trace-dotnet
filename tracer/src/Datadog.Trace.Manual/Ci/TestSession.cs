// <copyright file="TestSession.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Ci.Stubs;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test session
/// </summary>
public static class TestSession
{
    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <returns>New test session instance</returns>
    public static ITestSession GetOrCreate(string command)
        => InternalGetOrCreate(command, workingDirectory: null, framework: null, startDate: null);

    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <param name="workingDirectory">Test session working directory</param>
    /// <returns>New test session instance</returns>
    public static ITestSession GetOrCreate(string command, string workingDirectory)
        => InternalGetOrCreate(command, workingDirectory, framework: null, startDate: null);

    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <param name="workingDirectory">Test session working directory</param>
    /// <param name="framework">Testing framework name</param>
    /// <returns>New test session instance</returns>
    public static ITestSession GetOrCreate(string command, string workingDirectory, string framework)
        => InternalGetOrCreate(command, workingDirectory, framework, startDate: null);

    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <param name="workingDirectory">Test session working directory</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="startDate">Test session start date</param>
    /// <returns>New test session instance</returns>
    public static ITestSession GetOrCreate(string command, string workingDirectory, string framework, DateTimeOffset startDate)
        => InternalGetOrCreate(command, workingDirectory, framework, startDate);

    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <param name="workingDirectory">Test session working directory</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="startDate">Test session start date</param>
    /// <param name="propagateEnvironmentVariables">Propagate session data through environment variables (out of proc session)</param>
    /// <returns>New test session instance</returns>
    public static ITestSession GetOrCreate(string command, string? workingDirectory, string? framework, DateTimeOffset? startDate, bool propagateEnvironmentVariables)
        => InternalGetOrCreate(command, workingDirectory, framework, startDate, propagateEnvironmentVariables);

    [Instrumented]
    internal static ITestSession InternalGetOrCreate(string command, string? workingDirectory, string? framework, DateTimeOffset? startDate, bool propagateEnvironmentVariables = false)
        => NullTestSession.Instance;
}
