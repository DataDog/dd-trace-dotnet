// <copyright file="IOperationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.Processing.IOperationContext interface for ducktyping
    /// </summary>
    internal interface IOperationContext
    {
        ///// <summary>
        ///// Gets the operation Id
        ///// </summary>
        string Id { get; }

        ///// <summary>
        ///// Gets the operation type (Query, Mutation, Subscription)
        ///// </summary>
        OperationTypeProxy Type { get; }

        ///// <summary>
        ///// Gets the operation Document
        ///// </summary>
        object Document { get; }

        ///// <summary>
        ///// Gets the operation Result
        ///// </summary>
        IResultHelper Result { get; }
    }
}
