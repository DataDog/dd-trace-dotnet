// <copyright file="ArgumentProxyV2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net.ASM;

/// <summary>
/// GraphQL.Language.AST.Argument interface for ducktyping
/// This object is for GraphQL version 2
/// </summary>
[DuckCopy]
internal struct ArgumentProxyV2
{
    public object Value;

    public NameNodeProxy NamedNode;
}
