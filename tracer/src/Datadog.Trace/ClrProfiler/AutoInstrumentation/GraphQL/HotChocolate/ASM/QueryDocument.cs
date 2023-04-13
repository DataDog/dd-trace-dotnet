// <copyright file="QueryDocument.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate.ASM.AST;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate.ASM
{
    /// <summary>
    /// HotChocolate.Language.QueryDocument interface for ducktyping
    /// https://github.com/ChilliCream/graphql-platform/blob/423e8a3285f4d20291bc78ce09fed23a091a01d0/src/HotChocolate/Core/src/Abstractions/Execution/QueryDocument.cs
    /// </summary>
    [DuckCopy]
    internal struct QueryDocument
    {
        public DocumentNode Document;
    }
}
