﻿// <copyright file="DocumentV5Struct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL;

/// <summary>
/// GraphQLParser.AST.GraphQLDocument proxy for ducktyping
/// https://github.com/graphql-dotnet/parser/blob/efb83a9f4054c0752cfeaac1e3c6b7cde5fa5607/src/GraphQLParser/AST/GraphQLDocument.cs
/// </summary>
[DuckCopy]
internal struct DocumentV5Struct
{
    public object Source;
}
