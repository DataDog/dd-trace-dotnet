// <copyright file="ConfigurationStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.AppSec.Rcm.Models.AsmDd;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Serilog;
using Action = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;

namespace Datadog.Trace.AppSec.Rcm;

internal record ConfigurationStatus
{
    internal const string WafRulesKey = "rules";
    internal const string WafRulesOverridesKey = "rules_override";
    internal const string WafExclusionsKey = "exclusions";
    internal const string WafRulesDataKey = "rules_data";
    internal const string WafCustomRulesKey = "custom_rules";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConfigurationStatus>();

    private readonly string? _embeddedRulesPath;

    public ConfigurationStatus(string? embeddedRulesPath) => _embeddedRulesPath = embeddedRulesPath;

    internal RuleSet? FallbackEmbeddedRuleSet { get; set; }

    internal bool? EnableAsm { get; set; } = null;

    internal Dictionary<string, RuleOverride[]> RulesOverridesByFile { get; } = new();

    internal Dictionary<string, RuleData[]> RulesDataByFile { get; } = new();

    internal Dictionary<string, JArray> ExclusionsByFile { get; } = new();

    internal Dictionary<string, RuleSet> RulesByFile { get; } = new();

    internal Dictionary<string, AsmFeature> AsmFeaturesByFile { get; } = new();

    internal Dictionary<string, JArray> CustomRulesByFile { get; } = new();

    internal IncomingUpdateStatus IncomingUpdateState { get; } = new();

    /// <summary>
    /// Gets or sets actions to take according to a waf result, these arent sent to the waf
    /// </summary>
    internal IDictionary<string, Action> Actions { get; set; } = new Dictionary<string, Action>();

    internal static List<RuleData> MergeRuleData(IEnumerable<RuleData> res)
    {
        if (res == null)
        {
            throw new ArgumentNullException(nameof(res));
        }

        var finalRuleData = new List<RuleData>();
        var groups = res.GroupBy(r => r.Id + r.Type);
        foreach (var rulesData in groups)
        {
            var dataByValue = rulesData.SelectMany(d => d.Data!).GroupBy(d => d.Value);
            var mergedData = new List<Data>();
            foreach (var data in dataByValue)
            {
                var longestLastingIp = data.OrderByDescending(d => d.Expiration ?? long.MaxValue).First();
                mergedData.Add(longestLastingIp);
            }

            var ruleData = rulesData.FirstOrDefault();
            if (ruleData != null && !string.IsNullOrEmpty(ruleData.Type) && !string.IsNullOrEmpty(ruleData.Id))
            {
                ruleData.Data = mergedData.ToArray();
                finalRuleData.Add(ruleData);
            }
        }

        return finalRuleData;
    }

    internal Dictionary<string, object> BuildDictionaryForWafAccordingToIncomingUpdate()
    {
        var dictionary = new Dictionary<string, object>();

        if (IncomingUpdateState.WafKeysToApply.Contains(WafExclusionsKey))
        {
            var exclusions = ExclusionsByFile.SelectMany(x => x.Value).ToList();
            dictionary.Add(WafExclusionsKey, new JArray(exclusions));
        }

        if (IncomingUpdateState.WafKeysToApply.Contains(WafRulesOverridesKey))
        {
            var overrides = RulesOverridesByFile.SelectMany(x => x.Value).ToList();
            dictionary.Add(WafRulesOverridesKey, overrides.Select(r => r.ToKeyValuePair()).ToArray());
        }

        if (IncomingUpdateState.WafKeysToApply.Contains(WafRulesDataKey))
        {
            var rulesData = MergeRuleData(RulesDataByFile.SelectMany(x => x.Value));
            dictionary.Add(WafRulesDataKey, rulesData.Select(r => r.ToKeyValuePair()).ToArray());
        }

        if (IncomingUpdateState.WafKeysToApply.Contains(WafCustomRulesKey))
        {
            var customRules = CustomRulesByFile.SelectMany(x => x.Value).ToList();
            var mergedCustomRules = new JArray(customRules);
            dictionary.Add(WafCustomRulesKey, mergedCustomRules);
        }

        if (IncomingUpdateState.FallbackToEmbeddedRulesetAtNextUpdate)
        {
            if (FallbackEmbeddedRuleSet == null)
            {
                var result = WafConfigurator.DeserializeEmbeddedOrStaticRules(_embeddedRulesPath);
                if (result != null)
                {
                    FallbackEmbeddedRuleSet = RuleSet.From(result);
                }
            }

            FallbackEmbeddedRuleSet?.AddToDictionaryAtRoot(dictionary);
        }
        else if (IncomingUpdateState.WafKeysToApply.Contains(WafRulesKey))
        {
            var rulesetFromRcm = RulesByFile.Values.FirstOrDefault();
            rulesetFromRcm?.AddToDictionaryAtRoot(dictionary);
        }

        return dictionary;
    }

    public void ResetUpdateMarkers() => IncomingUpdateState.Reset();

    internal record IncomingUpdateStatus
    {
        internal HashSet<string> WafKeysToApply { get; } = new();

        internal bool FallbackToEmbeddedRulesetAtNextUpdate { get; private set; }

        internal bool SecurityStateChange { get; set; }

        public void Reset()
        {
            FallbackToEmbeddedRulesetAtNextUpdate = false;
            WafKeysToApply.Clear();
            SecurityStateChange = false;
        }

        public void FallbackToEmbeddedRuleset() => FallbackToEmbeddedRulesetAtNextUpdate = true;

        public void SignalSecurityStateChange() => SecurityStateChange = true;
    }
}
