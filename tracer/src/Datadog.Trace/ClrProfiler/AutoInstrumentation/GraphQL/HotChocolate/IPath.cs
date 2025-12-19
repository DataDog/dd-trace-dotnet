// <copyright file="IPath.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Path interface for ducktyping
    /// Represents HotChocolate.Path class (available in v11+)
    /// </summary>
    internal interface IPath
    {
        /// <summary>
        /// Creates a new list representing the current Path.
        /// Returns IReadOnlyList containing path segments (strings for field names, ints for array indices)
        /// </summary>
        IReadOnlyList<object> ToList();
    }
}
