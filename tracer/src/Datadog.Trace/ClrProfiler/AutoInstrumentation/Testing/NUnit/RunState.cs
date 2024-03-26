// <copyright file="RunState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// The RunState enum indicates whether a test can be executed.
    /// </summary>
    internal enum RunState
    {
        /// <summary>
        /// The test is not runnable.
        /// </summary>
        NotRunnable,

        /// <summary>
        /// The test is runnable.
        /// </summary>
        Runnable,

        /// <summary>
        /// The test can only be run explicitly
        /// </summary>
        Explicit,

        /// <summary>
        /// The test has been skipped. This value may
        /// appear on a Test when certain attributes
        /// are used to skip the test.
        /// </summary>
        Skipped,

        /// <summary>
        /// The test has been ignored. May appear on
        /// a Test, when the IgnoreAttribute is used.
        /// </summary>
        Ignored
    }
}
