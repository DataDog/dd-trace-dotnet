// <copyright file="GraphQLFieldProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

#pragma warning disable SA1302 // Interface names should begin with I

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net.ASM;

/// <summary>
/// GraphQLParser.AST.GraphQLField interface for ducktyping
/// https://github.com/graphql-dotnet/parser/blob/efb83a9f4054c0752cfeaac1e3c6b7cde5fa5607/src/GraphQLParser/AST/GraphQLField.cs
/// </summary>
[DuckCopy]
internal struct GraphQLFieldProxy
{
    #nullable enable
    public IEnumerable<object>? Arguments;
}
