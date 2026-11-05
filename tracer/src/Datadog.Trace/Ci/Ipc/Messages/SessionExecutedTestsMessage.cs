// <copyright file="SessionExecutedTestsMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Ipc.Messages;

/// <summary>
/// Number of tests executed by a test runner, sent to the test session that owns the run.
/// </summary>
internal sealed class SessionExecutedTestsMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionExecutedTestsMessage"/> class for IPC deserialization.
    /// </summary>
    public SessionExecutedTestsMessage()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionExecutedTestsMessage"/> class.
    /// </summary>
    /// <param name="executedTests">Number of executed tests, or null when the runner could not count them.</param>
    public SessionExecutedTestsMessage(long? executedTests)
    {
        ExecutedTests = executedTests;
    }

    /// <summary>
    /// Gets or sets the number of executed tests, or null when the runner could not count them.
    /// </summary>
    [JsonProperty("executed_tests")]
    public long? ExecutedTests { get; set; }
}
