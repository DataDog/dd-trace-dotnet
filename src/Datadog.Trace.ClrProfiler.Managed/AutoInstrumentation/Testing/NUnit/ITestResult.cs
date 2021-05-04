// <copyright file="ITestResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.TestResult
    /// </summary>
    public interface ITestResult
    {
        /// <summary>
        /// Gets the test with which this result is associated.
        /// </summary>
        ITest Test { get; }
    }
}
