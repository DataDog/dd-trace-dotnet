// <copyright file="CallTargetAotDuckTypePlan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Tools.Runner.DuckTypeAot;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Represents the complete set of DuckType mappings implied by the current CallTarget selection.
/// </summary>
internal sealed class CallTargetAotDuckTypePlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotDuckTypePlan"/> class.
    /// </summary>
    public CallTargetAotDuckTypePlan(IReadOnlyList<DuckTypeAotMapping> mappings)
    {
        Mappings = mappings;
    }

    /// <summary>
    /// Gets the canonical forward DuckType mappings required by the selected CallTarget bindings.
    /// </summary>
    public IReadOnlyList<DuckTypeAotMapping> Mappings { get; }
}
