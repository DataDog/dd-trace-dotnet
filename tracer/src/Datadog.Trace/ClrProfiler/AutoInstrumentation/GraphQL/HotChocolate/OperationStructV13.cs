// <copyright file="OperationStructV13.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.Processing.IOperation interface for ducktyping
    /// https://github.com/ChilliCream/graphql-platform/blob/35301472065248ce4e2f34894041f39124e3c7b8/src/HotChocolate/Core/src/Types/Execution/Processing/IOperation.cs
    /// </summary>
    [DuckCopy]
    internal struct OperationStructV13
    {
        ///// <summary>
        ///// Gets the operation type (Query, Mutation, Subscription)
        ///// </summary>
        [Duck(Name = "Type")]
        public OperationTypeProxy OperationType;

        public string Name;
    }
}
