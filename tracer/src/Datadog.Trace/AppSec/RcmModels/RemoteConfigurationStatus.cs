// <copyright file="RemoteConfigurationStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Datadog.Trace.AppSec.RcmModels.Asm;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Action = Datadog.Trace.AppSec.RcmModels.Asm.Action;

namespace Datadog.Trace.AppSec.RcmModels;

internal class RemoteConfigurationStatus
{
    internal bool? EnableAsm { get; set; } = null;

    internal Dictionary<string, RuleOverride[]> RulesOverridesByFile { get; } = new();

    internal Dictionary<string, RuleData[]> RulesDataByFile { get; } = new();

    internal Dictionary<string, JArray> ExclusionsByFile { get; } = new();

    /// <summary>
    /// Gets or sets actions to take according to a waf result, these arent sent to the waf
    /// </summary>
    internal IDictionary<string, Action> Actions { get; set; } = new Dictionary<string, Action>();

    internal string RemoteRulesJson { get; set; }

    internal static List<RuleData> MergeRuleData(IEnumerable<RuleData> res)
    {
        if (res == null)
        {
            throw new ArgumentNullException(nameof(res));
        }

        var finalRuleDatas = new List<RuleData>();
        var groups = res.GroupBy(r => r.Id + r.Type);
        foreach (var ruleDatas in groups)
        {
            var datasByValue = ruleDatas.SelectMany(d => d.Data!).GroupBy(d => d.Value);
            var mergedDatas = new List<Data>();
            foreach (var data in datasByValue)
            {
                var longestLastingIp = data.OrderByDescending(d => d.Expiration ?? long.MaxValue).First();
                mergedDatas.Add(longestLastingIp);
            }

            var ruleData = ruleDatas.FirstOrDefault();
            if (ruleData != null && !string.IsNullOrEmpty(ruleData.Type) && !string.IsNullOrEmpty(ruleData.Id))
            {
                ruleData.Data = mergedDatas.ToArray();
                finalRuleDatas.Add(ruleData);
            }
        }

        return finalRuleDatas;
    }

    internal Dictionary<string, object> ToDictionary()
    {
        var overrides = RulesOverridesByFile.SelectMany(x => x.Value).ToList();
        var exclusions = ExclusionsByFile.SelectMany(x => x.Value).ToList();
        var rulesData = MergeRuleData(RulesDataByFile.SelectMany(x => x.Value));
        var dictionary = new Dictionary<string, object> { { "rules_override", overrides.Select(r => r.ToKeyValuePair()).ToArray() }, { "exclusions", new JArray(exclusions) }, { "rules_data", rulesData.Select(r => r.ToKeyValuePair()).ToArray() } };

        if (!string.IsNullOrEmpty(RemoteRulesJson))
        {
            dictionary.Add("rules", RemoteRulesJson);
        }

        return dictionary;
    }
}
