// <copyright file="IOperation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    /// <summary>
    /// GraphQL.Language.AST.Operation interface for ducktyping
    /// </summary>
    internal interface IOperation
    {
        /// <summary>
        /// Gets the name of the operation
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the type of the operation
        /// </summary>
        OperationTypeProxy OperationType { get; }
    }
}
