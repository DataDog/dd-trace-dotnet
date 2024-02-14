// <copyright file="GraphQLArgumentStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    /// <summary>
    /// GraphQLParser.AST.GraphQLName
    /// https://github.com/graphql-dotnet/parser/blob/v8/src/GraphQLParser/AST/GraphQLArgument.cs
    /// </summary>
    [DuckCopy]
    internal struct GraphQLArgumentStruct
    {
        public GraphQLNameStructV5AndV7 Name;
        public object Value;
    }
}
