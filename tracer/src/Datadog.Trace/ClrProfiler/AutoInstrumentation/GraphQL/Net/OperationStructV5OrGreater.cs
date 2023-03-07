// <copyright file="OperationStructV5OrGreater.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    /// <summary>
    /// GraphQLParser.AST.GraphQLOperationDefinition
    /// https://github.com/graphql-dotnet/parser/blob/efb83a9f4054c0752cfeaac1e3c6b7cde5fa5607/src/GraphQLParser/AST/Definitions/GraphQLOperationDefinition.cs
    /// </summary>
    [DuckCopy]
    internal struct OperationStructV5OrGreater
    {
        public GraphQLNameStructV5OrGreater Name;
        public OperationTypeProxy Operation;
    }
}
