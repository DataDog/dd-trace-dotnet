// <copyright file="ITestSession.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test session
/// </summary>
[DuckType("Datadog.Trace.Ci.TestSession", "Datadog.Trace")]
[DuckAsClass]
public interface ITestSession
{
    /// <summary>
    /// Gets the session command
    /// </summary>
    string? Command { get; }

    /// <summary>
    /// Gets the session command working directory
    /// </summary>
    string? WorkingDirectory { get; }

    /// <summary>
    /// Gets the test session start date
    /// </summary>
    DateTimeOffset StartTime { get; }

    /// <summary>
    /// Gets the test framework
    /// </summary>
    string? Framework { get; }

    /// <summary>
    /// Sets a string tag into the test
    /// </summary>
    /// <param name="key">Key of the tag</param>
    /// <param name="value">Value of the tag</param>
    void SetTag(string key, string? value);

    /// <summary>
    /// Sets a number tag into the test
    /// </summary>
    /// <param name="key">Key of the tag</param>
    /// <param name="value">Value of the tag</param>
    void SetTag(string key, double? value);

    /// <summary>
    /// Set Error Info
    /// </summary>
    /// <param name="type">Error type</param>
    /// <param name="message">Error message</param>
    /// <param name="callStack">Error callstack</param>
    void SetErrorInfo(string type, string message, string? callStack);

    /// <summary>
    /// Set Error Info from Exception
    /// </summary>
    /// <param name="exception">Exception instance</param>
    void SetErrorInfo(Exception exception);

    /// <summary>
    /// Close test module
    /// </summary>
    /// <param name="status">Test session status</param>
    void Close(TestStatus status);

    /// <summary>
    /// Close test module
    /// </summary>
    /// <param name="status">Test session status</param>
    /// <param name="duration">Duration of the test module</param>
    void Close(TestStatus status, TimeSpan? duration);

    /// <summary>
    /// Close test module
    /// </summary>
    /// <param name="status">Test session status</param>
    /// <returns>Task instance</returns>
    Task CloseAsync(TestStatus status);

    /// <summary>
    /// Close test module
    /// </summary>
    /// <param name="status">Test session status</param>
    /// <param name="duration">Duration of the test module</param>
    /// <returns>Task instance</returns>
    Task CloseAsync(TestStatus status, TimeSpan? duration);

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <returns>New test module instance</returns>
    ITestModule CreateModule(string name);

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <returns>New test module instance</returns>
    ITestModule CreateModule(string name, string framework, string frameworkVersion);

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <param name="startDate">Test session start date</param>
    /// <returns>New test module instance</returns>
    ITestModule CreateModule(string name, string framework, string frameworkVersion, DateTimeOffset startDate);
}
