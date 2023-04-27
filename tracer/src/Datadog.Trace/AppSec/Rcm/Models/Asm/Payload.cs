// <copyright file="Payload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Rcm.Models.Asm;

internal class Payload
{
    [JsonProperty("rules_override")]
    public RuleOverride[]? RuleOverrides { get; set; }

    [JsonProperty("actions")]
    public Action[]? Actions { get; set; }

    [JsonProperty("exclusions")]
    public JArray? Exclusions { get; set; }

    [JsonProperty("custom_rules")]
    public JArray? CustomRules { get; set; }
}
