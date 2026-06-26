// <copyright file="DuckTypeAotScenarioInventoryParseResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Represents duck type aot scenario inventory parse result.
    /// </summary>
    internal sealed class DuckTypeAotScenarioInventoryParseResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotScenarioInventoryParseResult"/> class.
        /// </summary>
        /// <param name="requiredScenarios">The required scenarios value.</param>
        /// <param name="errors">The errors value.</param>
        public DuckTypeAotScenarioInventoryParseResult(IEnumerable<string> requiredScenarios, IReadOnlyList<string> errors)
        {
            RequiredScenarios = new List<string>(requiredScenarios);
            Errors = errors;
        }

        /// <summary>
        /// Gets required scenarios.
        /// </summary>
        /// <value>The required scenarios value.</value>
        public IReadOnlyList<string> RequiredScenarios { get; }

        /// <summary>
        /// Gets errors.
        /// </summary>
        /// <value>The errors value.</value>
        public IReadOnlyList<string> Errors { get; }
    }
}
