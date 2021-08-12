// <copyright file="ITestResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.TestResult
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface ITestResult
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
        string Message { get; }

        /// <summary>
        /// Gets any stacktrace associated with an error or failure.
        /// </summary>
        string StackTrace { get; }
    }
}
