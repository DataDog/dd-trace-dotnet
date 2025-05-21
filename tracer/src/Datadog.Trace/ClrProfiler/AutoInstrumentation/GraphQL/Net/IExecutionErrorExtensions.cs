// <copyright file="IExecutionErrorExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    /// <summary>
    /// GraphQL.ExecutionError interface for ducktyping with Extensions
    /// </summary>
    internal interface IExecutionErrorExtensions : IExecutionError
    {
        /// <summary>
        /// Gets additional Extensions information about error.
        /// </summary>
        Dictionary<string, object> Extensions { get; }
    }
}
