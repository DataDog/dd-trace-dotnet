// <copyright file="OperationStructV16.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.Processing.Operation class for ducktyping
    /// https://github.com/ChilliCream/graphql-platform/blob/16.1.4/src/HotChocolate/Core/src/Types/Execution/Processing/Operation.cs
    /// In v16 the Type property was renamed to Kind
    /// </summary>
    [DuckCopy]
    internal struct OperationStructV16
    {
        /// <summary>
        /// Gets the operation type (Query, Mutation, Subscription)
        /// </summary>
        public OperationTypeProxy Kind;

        public string? Name;
    }
}
