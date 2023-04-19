// <copyright file="IExecutionContextVariable.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    /// <summary>
    /// GraphQL.Execution.ExecutionContext interface for ducktyping
    /// </summary>
    internal interface IExecutionContextVariable
    {
        /// <summary>
        /// Gets the variables
        /// </summary>
        IEnumerable<object> Variables { get; }
    }
}
