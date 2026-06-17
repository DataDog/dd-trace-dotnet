// <copyright file="Split.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class Split
{
    public List<Shard>? Shards { get; set; }

    public string? VariationKey { get; set; }

    public Dictionary<string, string>? ExtraLogging { get; set; }

    // Serial id of the experiment split, used for APM span enrichment.
    // Nullable: absent in UFC shapes that predate span enrichment. Deserialized
    // from the UFC "serialId" field (Newtonsoft case-insensitive matching).
    public long? SerialId { get; set; }
}
