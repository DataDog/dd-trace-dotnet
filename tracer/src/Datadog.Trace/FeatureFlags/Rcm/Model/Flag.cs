// <copyright file="Flag.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal class Flag
{
    public string? Key { get; set; }

    public bool? Enabled { get; set; }

    public ValueType? VariationType { get; set; }

    public Dictionary<string, Variant>? Variations { get; set; }

    public List<Allocation>? Allocations { get; set; }
}
