// <copyright file="IExecutionContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
{
    /// <summary>
    /// GraphQL.Execution.ExecutionContext interface for ducktyping
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IExecutionContext
    {
        /// <summary>
        /// Gets the document associated with the execution context
        /// </summary>
        IDocument Document { get; }

        /// <summary>
        /// Gets the operation associated with the execution context
        /// </summary>
        IOperation Operation { get; }

        /// <summary>
        /// Gets the execution errors
        /// </summary>
        IExecutionErrors Errors { get; }
    }
}
