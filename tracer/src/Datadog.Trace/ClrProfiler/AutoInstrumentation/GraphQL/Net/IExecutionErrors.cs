// <copyright file="IExecutionErrors.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    /// <summary>
    /// GraphQL.ExecutionErrors interface for ducktyping
    /// </summary>
    internal interface IExecutionErrors
    {
        /// <summary>
        /// Gets the number of errors
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets the ExecutionError at the specified index
        /// </summary>
        /// <param name="index">Index to lookup</param>
        /// <returns>An execution error</returns>
        IExecutionError this[int index] { get; }
    }
}
