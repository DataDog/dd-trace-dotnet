// <copyright file="IExecutionResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.IExecutionResult interface for ducktyping
    /// </summary>
    internal interface IExecutionResult
    {
        /// <summary>
        /// Gets the executing operation errors
        /// </summary>
        IEnumerable Errors { get; }
    }
}
