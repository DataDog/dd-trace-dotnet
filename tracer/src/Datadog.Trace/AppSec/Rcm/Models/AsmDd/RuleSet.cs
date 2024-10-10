// <copyright file="RuleSet.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Rcm.Models.AsmDd;

internal class RuleSet
{
    internal string? Version { get; set; }

    internal JToken? Metadata { get; set; }

    [JsonProperty("rules")]
    internal JToken? Rules { get; set; }

    [JsonProperty("processors")]
    internal JToken? Processors { get; set; }

    [JsonProperty("actions")]
    internal JToken? Actions { get; set; }

    [JsonProperty("scanners")]
    internal JToken? Scanners { get; set; }

    [JsonProperty("exclusions")]
    internal JToken? Exclusions { get; set; }

    [JsonProperty("custom_rules")]
    internal JToken? CustomRules { get; set; }

    public static RuleSet From(JToken result)
    {
        // can rules from rc contains exclusions and custom rules?

        var ruleset = new RuleSet
        {
            Version = result["version"]?.ToString(),
            Metadata = result["metadata"],
            Rules = result["rules"],
            Processors = result["processors"],
            Scanners = result["scanners"],
            Actions = result["actions"],
            Exclusions = result["exclusions"],
            CustomRules = result["custom_rules"]
        };
        return ruleset;
    }

    /// <summary>
    /// on the contrary to other data, this one should be added at the root because it's shaped as {"version": "2.2","metadata": {"rules_version": "1.3.2"},"rules": []}
    /// </summary>
    /// <param name="dictionary">dictionary</param>
    public void AddToDictionaryAtRoot(Dictionary<string, object> dictionary)
    {
        if (Rules != null)
        {
            dictionary["rules"] = Rules;
        }

        if (Metadata != null)
        {
            dictionary["metadata"] = Metadata;
        }

        if (Version != null)
        {
            dictionary["version"] = Version;
        }

        if (Processors != null)
        {
            dictionary["processors"] = Processors;
        }

        if (Scanners != null)
        {
            dictionary["scanners"] = Scanners;
        }

        if (Exclusions is not null)
        {
            dictionary["exclusions"] = Exclusions;
        }

        if (CustomRules is not null)
        {
            dictionary["custom_rules"] = CustomRules;
        }
    }
}
