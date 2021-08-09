// <copyright file="ITestExecutionContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.TestExecutionContext
    /// </summary>
    public interface ITestExecutionContext
    {
        /// <summary>
        /// Gets the current test
        /// </summary>
        ITest CurrentTest { get; }
    }
}
