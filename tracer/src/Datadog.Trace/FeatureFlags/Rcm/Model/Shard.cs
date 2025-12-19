// <copyright file="Shard.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Iast;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class Shard
{
    public string? Salt { get; set; }

    public List<ShardRange>? Ranges { get; set; }

    public int TotalShards { get; set; }
}
