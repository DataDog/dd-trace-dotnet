// <copyright file="PreparedOperationStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal.DuckTyping;

namespace Datadog.Trace.Internal.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.Processing.IPreparedOperation interface for ducktyping
    /// </summary>
    [DuckCopy]
    internal struct PreparedOperationStruct
    {
        ///// <summary>
        ///// Gets the operation type (Query, Mutation, Subscription)
        ///// </summary>
        [Duck(Name = "Type")]
        public OperationTypeProxy OperationType;

        public NameStringProxy Name;
    }
}
