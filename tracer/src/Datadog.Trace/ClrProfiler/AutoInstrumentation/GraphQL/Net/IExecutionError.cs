// <copyright file="IExecutionError.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    /// <summary>
    /// GraphQL.ExecutionError interface for ducktyping
    /// </summary>
    internal interface IExecutionError
    {
        /// <summary>
        /// Gets a code for the error
        /// </summary>
        string Code { get; }

        /// <summary>
        /// Gets a list of locations in the document where the error applies
        /// </summary>
        // See comment for Path
        IEnumerable Locations { get; }

        /// <summary>
        /// Gets a message for the error
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Gets the path in the document where the error applies
        /// </summary>
        // GraphQL2 uses IEnumerable<string> and GraphQL3 uses IEnumerable<object>
        // Declaring the property as IEnumerable allows to support both at once
        IEnumerable Path { get; }
    }
}
