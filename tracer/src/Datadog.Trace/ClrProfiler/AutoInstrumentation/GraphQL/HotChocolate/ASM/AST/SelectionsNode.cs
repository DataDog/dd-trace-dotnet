// <copyright file="SelectionsNode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate.ASM.AST
{
    /// <summary>
    /// Syntax node with a SelectionSet attribute ducktyping
    /// </summary>
    [DuckCopy]
    internal struct SelectionsNode
    {
        public SelectionSetNode SelectionSet;
    }
}
