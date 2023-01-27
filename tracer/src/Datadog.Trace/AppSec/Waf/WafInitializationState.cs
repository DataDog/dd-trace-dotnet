// <copyright file="WafInitializationState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Waf;

internal class WafInitializationState
{
    public static readonly WafInitializationState Empty = new WafInitializationState(string.Empty, new JArray(), new Dictionary<string, List<string>>());

    private WafInitializationState(string remoteRulesJson, JArray exclusions, Dictionary<string, List<string>> onMatch)
    {
        RemoteRulesJson = remoteRulesJson;
        Exclusions = exclusions;
        OnMatch = onMatch;
    }

    public string RemoteRulesJson { get; }

    public bool AreRemoteRulesJsonAvailable => !string.IsNullOrEmpty(RemoteRulesJson);

    public JArray Exclusions { get; }

    public Dictionary<string, bool> RuleStatus { get; }

    public Dictionary<string, List<string>> OnMatch { get; }

    public WafInitializationState WithRules(string rules)
    {
        return new WafInitializationState(rules, Exclusions, OnMatch);
    }

    public WafInitializationState WithExclusionsAndOnMatch(JArray exclusions, Dictionary<string, List<string>> onMatch)
    {
        return new WafInitializationState(RemoteRulesJson, exclusions, onMatch);
    }
}
