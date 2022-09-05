// <copyright file="IExecutionContextV5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    /// <summary>
    /// GraphQL.Execution.ExecutionContext interface for ducktyping
    /// </summary>
    internal interface IExecutionContextV5
    {
        /// <summary>
        /// Gets the document associated with the execution context
        /// </summary>
        DocumentV5Struct Document { get; }

        /// <summary>
        /// Gets the operation associated with the execution context
        /// </summary>
        OperationV5Struct Operation { get; }

        /// <summary>
        /// Gets the execution errors
        /// </summary>
        IExecutionErrors Errors { get; }
    }
}
