// <copyright file="RuleData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.AppSec.Rcm.Models.AsmData;

internal class RuleData
{
    public string? Type { get; set; }

    public string? Id { get; set; }

    public Data[]? Data { get; set; }

    public List<KeyValuePair<string, object?>> ToKeyValuePair() => new() { new("type", Type), new("id", Id), new("data", Data?.Select(d => d.ToKeyValuePair()).ToArray()) };
}
