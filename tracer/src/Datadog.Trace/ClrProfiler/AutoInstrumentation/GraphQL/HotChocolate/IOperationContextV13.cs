// <copyright file="IOperationContextV13.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.Processing.OperationContext struct for ducktyping
    /// https://github.com/ChilliCream/graphql-platform/blob/35301472065248ce4e2f34894041f39124e3c7b8/src/HotChocolate/Core/src/Execution/Processing/OperationContext.Execution.cs
    /// </summary>
    internal interface IOperationContextV13
    {
        ///// <summary>
        ///// Gets the context operation
        ///// </summary>
        public OperationStructV13 Operation { get; }
    }
}
