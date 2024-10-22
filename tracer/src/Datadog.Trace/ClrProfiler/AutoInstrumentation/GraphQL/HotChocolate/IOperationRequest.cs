// <copyright file="IOperationRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.IOperationRequest interface for ducktyping
    /// </summary>
    internal interface IOperationRequest
    {
        /// <summary>
        /// Gets the document
        /// </summary>
        object Document { get; }

        /// <summary>
        /// Gets the operation name
        /// </summary>
        public string? OperationName { get; }
    }
}
