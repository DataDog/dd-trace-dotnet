// <copyright file="ITestExecutionContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.TestExecutionContext
    /// </summary>
    internal interface ITestExecutionContext
    {
        /// <summary>
        /// Gets the current test
        /// </summary>
        ITest CurrentTest { get; }

        /// <summary>
        /// Gets or sets the current result
        /// </summary>
        ITestResult CurrentResult { get; set; }

        /// <summary>
        /// Gets or sets the current repeat count
        /// </summary>
        int CurrentRepeatCount { get; set; }

        /// <summary>
        /// Gets or sets the current test object
        /// </summary>
        object TestObject { get; set; }
    }
}
