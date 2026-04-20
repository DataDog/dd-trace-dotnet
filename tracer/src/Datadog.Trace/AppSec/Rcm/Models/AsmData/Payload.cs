// <copyright file="Payload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Rcm.Models.AsmData;

internal sealed class Payload
{
    [JsonProperty("rules_data")]
    public RuleData[]? RulesData { get; set; }

    [JsonProperty("exclusion_data")]
    public RuleData[]? ExclusionsData { get; set; }
}
