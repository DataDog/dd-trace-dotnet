// <copyright file="IOperationProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Resolvers.IOperation interface for ducktyping
    /// </summary>
    internal interface IOperationProxy
    {
        ///// <summary>
        ///// Gets the operation type (Query, Mutation, Subscription)
        ///// </summary>
        [Duck(Name = "Type")]
        OperationTypeProxy OperationType { get; }

        NameStringProxy? Name { get; }
    }
}
