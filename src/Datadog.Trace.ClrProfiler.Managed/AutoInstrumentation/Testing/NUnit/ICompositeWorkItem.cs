// <copyright file="ICompositeWorkItem.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.Execution.CompositeWorkItem
    /// </summary>
    public interface ICompositeWorkItem
    {
        /// <summary>
        /// Gets the List of Child WorkItems
        /// </summary>
        IEnumerable Children { get; }
    }
}
