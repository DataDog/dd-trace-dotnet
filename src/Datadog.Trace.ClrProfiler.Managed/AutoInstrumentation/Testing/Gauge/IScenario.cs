// <copyright file="IScenario.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Gauge
{
    /// <summary>
    /// Scenario ducktype interface
    /// </summary>
    public interface IScenario
    {
        /// <summary>
        /// Gets list of all the tags in the scenario
        /// </summary>
        IEnumerable<string> Tags { get; }

        /// <summary>
        /// Gets a value indicating whether the current scenario or step is failing due to error.
        /// </summary>
        bool IsFailing { get; }

        /// <summary>
        /// Gets name of the Scenario as mentioned in the scenario heading
        /// </summary>
        string Name { get; }
    }
}
