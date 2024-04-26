// <copyright file="ITestResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.IO;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.TestResult
    /// </summary>
    internal interface ITestResult : IDuckType
    {
        /// <summary>
        /// Gets the test with which this result is associated.
        /// </summary>
        ITest Test { get; }

        /// <summary>
        /// Gets the resultstate of the test result.
        /// </summary>
        IResultState ResultState { get; }

        /// <summary>
        /// Gets the message associated with a test failure.
        /// </summary>
        string? Message { get; }

        /// <summary>
        /// Gets any stacktrace associated with an error or failure.
        /// </summary>
        string StackTrace { get; }

        /// <summary>
        /// Gets or sets duration
        /// </summary>
        double Duration { get; set; }

        /// <summary>
        /// Gets or sets the time the test started running.
        /// </summary>
        DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the time the test finished running.
        /// </summary>
        DateTime EndTime { get; set; }

        /// <summary>
        /// Gets a TextWriter, which will write output to be included in the result.
        /// </summary>
        TextWriter OutWriter { get; }

        /// <summary>
        /// Gets any text output written to this result.
        /// </summary>
        string Output { get; }

        /// <summary>
        /// Gets a list of assertion results associated with the test.
        /// </summary>
        IList AssertionResults { get; }

        /// <summary>
        /// Record an exception for the test result
        /// </summary>
        /// <param name="ex">Exception instance</param>
        void RecordException(Exception ex);

        /// <summary>
        /// Set the result of the test
        /// </summary>
        /// <param name="resultState">The ResultState to use in the result</param>
        /// <param name="message">A message associated with the result state</param>
        /// <param name="stackTrace">Stack trace giving the location of the command</param>
        void SetResult(IResultState resultState, string? message, string? stackTrace);
    }
}
