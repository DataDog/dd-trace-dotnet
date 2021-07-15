// <copyright file="ISpecification.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Gauge
{
    /// <summary>
    /// Specification ducktype interface
    /// </summary>
    public interface ISpecification
    {
        /// <summary>
        /// Gets list of all the tags in the Spec
        /// </summary>
        IEnumerable<string> Tags { get; }

        /// <summary>
        /// Gets a value indicating whether true if the current spec is failing.
        /// </summary>
        bool IsFailing { get; }

        /// <summary>
        /// Gets full path to the Spec
        /// </summary>
        string FileName { get; }

        /// <summary>
        /// Gets the name of the Specification as mentioned in the Spec heading
        /// </summary>
        string Name { get; }
    }
}
