// <copyright file="IExecutionError.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
{
    /// <summary>
    /// GraphQL.ExecutionError interface for ducktyping
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IExecutionError
    {
        /// <summary>
        /// Gets a code for the error
        /// </summary>
        string Code { get; }

        /// <summary>
        /// Gets a list of locations in the document where the error applies
        /// </summary>
        IEnumerable<object> Locations { get; }

        /// <summary>
        /// Gets a message for the error
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Gets the path in the document where the error applies
        /// </summary>
        IEnumerable<string> Path { get; }
    }
}
