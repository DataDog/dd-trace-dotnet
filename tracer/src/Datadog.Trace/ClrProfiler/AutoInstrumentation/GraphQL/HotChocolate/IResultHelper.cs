// <copyright file="IResultHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.Processing.IResultHelper interface for ducktyping
    /// </summary>
    internal interface IResultHelper
    {
        /// <summary>
        /// Gets the executing operation context
        /// </summary>
        IOperationContext Context { get; }

        IReadOnlyList<IError> Errors { get; }
    }
}
