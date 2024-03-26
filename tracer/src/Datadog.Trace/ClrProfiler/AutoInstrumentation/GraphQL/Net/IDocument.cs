// <copyright file="IDocument.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    /// <summary>
    /// GraphQL.Language.AST.Document interface for ducktyping
    /// </summary>
    internal interface IDocument
    {
        /// <summary>
        /// Gets the original query from the document
        /// </summary>
        string OriginalQuery { get; }
    }
}
