// <copyright file="IHasValueNode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net;

/// <summary>
/// GraphQLParser.AST.IHasValueNode interface for ducktyping
/// </summary>
internal interface IHasValueNode
{
    /// <summary>
    /// Gets value of AST node represented as <see cref="object"/>.
    /// </summary>
    public object Value { get; }
}
