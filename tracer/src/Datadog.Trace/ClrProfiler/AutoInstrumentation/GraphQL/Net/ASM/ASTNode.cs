// <copyright file="ASTNode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net.ASM;

/// <summary>
/// GraphQLParser.AST.ASTNode interface for ducktyping
/// </summary>
[DuckCopy]
internal struct ASTNode
{
    public ASTNodeKindProxy Kind;
}
