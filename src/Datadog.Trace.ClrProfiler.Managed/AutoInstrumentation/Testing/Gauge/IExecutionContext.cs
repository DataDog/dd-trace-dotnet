// <copyright file="IExecutionContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Gauge
{
    /// <summary>
    /// ExecutionContext ducktype interface
    /// </summary>
    public interface IExecutionContext
    {
        /// <summary>
        /// Gets current specification
        /// </summary>
        ISpecification CurrentSpecification { get; }

        /// <summary>
        /// Gets current scenario
        /// </summary>
        IScenario CurrentScenario { get; }

        /// <summary>
        /// Gets current step
        /// </summary>
        IStepDetails CurrentStep { get; }

        /// <summary>
        /// Get all tags
        /// </summary>
        /// <returns>List of strings</returns>
        List<string> GetAllTags();
    }
}
