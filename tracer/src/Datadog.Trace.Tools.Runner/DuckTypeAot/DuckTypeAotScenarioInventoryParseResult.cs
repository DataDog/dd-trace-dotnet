// <copyright file="DuckTypeAotScenarioInventoryParseResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    internal sealed class DuckTypeAotScenarioInventoryParseResult
    {
        public DuckTypeAotScenarioInventoryParseResult(IEnumerable<string> requiredScenarios, IReadOnlyList<string> errors)
        {
            RequiredScenarios = new List<string>(requiredScenarios);
            Errors = errors;
        }

        public IReadOnlyList<string> RequiredScenarios { get; }

        public IReadOnlyList<string> Errors { get; }
    }
}
