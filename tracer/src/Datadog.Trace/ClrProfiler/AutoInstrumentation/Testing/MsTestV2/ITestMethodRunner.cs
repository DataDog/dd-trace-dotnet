// <copyright file="ITestMethodRunner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2
{
    /// <summary>
    /// TestMethodRunner ducktype interface
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface ITestMethodRunner
    {
        /// <summary>
        /// Gets the TestMethodInfo instance
        /// </summary>
        [DuckField(Name = "testMethodInfo")]
        ITestMethod TestMethodInfo { get; }
    }
}
