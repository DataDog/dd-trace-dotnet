// <copyright file="IExecutionNode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net.ASM;

/// <summary>
/// GraphQL.Execution.ExecutionNode interface for ducktyping
/// </summary>
internal interface IExecutionNode
{
    public GraphQLFieldProxy Field { get; }

    public string Name { get; }
}
