// <copyright file="TestStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// The TestStatus enum indicates the result of running a test
    /// </summary>
    public enum TestStatus
    {
        /// <summary>
        /// The test was inconclusive
        /// </summary>
        Inconclusive,

        /// <summary>
        /// The test has skipped
        /// </summary>
        Skipped,

        /// <summary>
        /// The test succeeded
        /// </summary>
        Passed,

        /// <summary>
        /// There was a warning
        /// </summary>
        Warning,

        /// <summary>
        /// The test failed
        /// </summary>
        Failed
    }
}
