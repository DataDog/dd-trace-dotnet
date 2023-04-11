// <copyright file="ArgumentProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net.ASM;

/// <summary>
/// GraphQL.Language.AST.Argument interface for ducktyping
/// https://github.com/graphql-dotnet/graphql-dotnet/blob/4da7f23c88f9df8e792b6beb47a0461948f18641/src/GraphQL/Language/AST/Argument.cs
/// </summary>
[DuckCopy]
internal struct ArgumentProxy
{
    public object Value;

    public NameNodeProxy NameNode;
}
