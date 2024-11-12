// <copyright file="ITestSuite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test suite
/// </summary>
[DuckType("Datadog.Trace.Ci.TestSuite", "Datadog.Trace")]
[DuckAsClass]
public interface ITestSuite
{
    /// <summary>
    /// Gets the test suite name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the test suite start date
    /// </summary>
    DateTimeOffset StartTime { get; }

    /// <summary>
    /// Gets the test module for this suite
    /// </summary>
    ITestModule Module { get; }

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
    /// Close test suite
    /// </summary>
    void Close();

    /// <summary>
    /// Close test suite
    /// </summary>
    /// <param name="duration">Duration of the test suite</param>
    void Close(TimeSpan? duration);

    /// <summary>
    /// Create a new test for this suite
    /// </summary>
    /// <param name="name">Name of the test</param>
    /// <returns>Test instance</returns>
    ITest CreateTest(string name);

    /// <summary>
    /// Create a new test for this suite
    /// </summary>
    /// <param name="name">Name of the test</param>
    /// <param name="startDate">Test start date</param>
    /// <returns>Test instance</returns>
    ITest CreateTest(string name, DateTimeOffset startDate);
}
