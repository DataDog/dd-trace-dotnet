// <copyright file="GraphQLValueListProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net.ASM;

/// <summary>
/// GraphQLValueProxy interface for ducktyping
/// </summary>
[DuckCopy]
internal struct GraphQLValueListProxy
{
    #nullable enable
    public List<ASTNode?> Values;
}
