// <copyright file="Allocation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class Allocation
{
    public string? Key { get; set; }

    public List<Rule>? Rules { get; set; }

    public string? StartAt { get; set; }

    public string? EndAt { get; set; }

    public List<Split>? Splits { get; set; }

    public bool? DoLog { get; set; }
}
