// <copyright file="IQueryRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.IQueryRequest interface for ducktyping
    /// </summary>
    internal interface IQueryRequest : IDuckType
    {
        object? Query { get; }

        public string? OperationName { get; }
    }
}
