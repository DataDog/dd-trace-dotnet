// <copyright file="IWorkScheduler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.Processing.WorkScheduler interface for ducktyping
    /// </summary>
    internal interface IWorkScheduler
    {
        /// <summary>
        /// Gets the executing operation context
        /// </summary>
        IOperationContext Context { get; }
    }
}
