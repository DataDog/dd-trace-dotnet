// <copyright file="UnitTestResultOutcome.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2
{
    /// <summary>
    /// UnitTestResult Outcome
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public enum UnitTestResultOutcome
    {
        /// <summary>
        /// Error
        /// </summary>
        Error,

        /// <summary>
        /// Failed
        /// </summary>
        Failed,

        /// <summary>
        /// Timeout
        /// </summary>
        Timeout,

        /// <summary>
        /// Inconclusive
        /// </summary>
        Inconclusive,

        /// <summary>
        /// Ignored
        /// </summary>
        Ignored,

        /// <summary>
        /// Not Runnable
        /// </summary>
        NotRunnable,

        /// <summary>
        /// Passed
        /// </summary>
        Passed,

        /// <summary>
        /// Not Found
        /// </summary>
        NotFound,

        /// <summary>
        /// In Progress
        /// </summary>
        InProgress,
    }
}
