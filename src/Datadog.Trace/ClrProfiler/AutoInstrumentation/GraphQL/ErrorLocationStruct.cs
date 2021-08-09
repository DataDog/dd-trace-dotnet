// <copyright file="ErrorLocationStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
{
    /// <summary>
    /// GraphQL.ErrorLocation interface for ducktyping
    /// </summary>
    [DuckCopy]
    public struct ErrorLocationStruct
    {
        /// <summary>
        /// Gets the line number of the document where the error occurred
        /// </summary>
        public int Line;

        /// <summary>
        /// Gets the column number of the document where the error occurred
        /// </summary>
        public int Column;
    }
}
