// <copyright file="OperationStructV14.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.Processing.IOperation interface for ducktyping
    /// https://github.com/ChilliCream/graphql-platform/blob/14.0.0/src/HotChocolate/Core/src/Types/Execution/Processing/IOperation.cs
    /// </summary>
    [DuckCopy]
    internal struct OperationStructV14
    {
        ///// <summary>
        ///// Gets the operation type (Query, Mutation, Subscription)
        ///// </summary>
        [Duck(Name = "Type")]
        public OperationTypeProxy OperationType;

        public string? Name;
    }
}
