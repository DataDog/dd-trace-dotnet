// <copyright file="DatabaseNamespaceStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Internal.DuckTyping;

namespace Datadog.Trace.Internal.ClrProfiler.AutoInstrumentation.MongoDb
{
    /// <summary>
    /// MongoDB.Driver.DatabaseNamespace interface for duck-typing
    /// </summary>
    [DuckCopy]
    internal struct DatabaseNamespaceStruct
    {
        /// <summary>
        /// Gets the name of the database
        /// </summary>
        public string? DatabaseName;
    }
}
