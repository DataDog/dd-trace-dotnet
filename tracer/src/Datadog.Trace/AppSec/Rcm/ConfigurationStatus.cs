// <copyright file="ConfigurationStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.AppSec.Rcm.Models.AsmDd;
using Datadog.Trace.AppSec.Rcm.Models.AsmFeatures;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Action = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;

namespace Datadog.Trace.AppSec.Rcm;

/// <summary>
/// This class represents the state of RCM for ASM.
/// It has 2 possible status:
/// - ASM is not activated, and _fileUpdates/_fileRemoves contain some pending non-deserialized changes to apply when ASM_FEATURES activate ASM. Every time an RC payload is received here, pending changes are reset to the last ones
/// - ASM is activated, stored configs in _fileUpdates/_fileRemoves are applied every time.
/// </summary>
internal record ConfigurationStatus
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConfigurationStatus>();

    internal const string WafRulesKey = "rules";
    internal const string WafRulesOverridesKey = "rules_override";
    internal const string WafExclusionsKey = "exclusions";
    internal const string WafRulesDataKey = "rules_data";
    internal const string WafCustomRulesKey = "custom_rules";
    internal const string WafActionsKey = "actions";
    private readonly IAsmConfigUpdater _asmFeatureProduct = new AsmFeaturesProduct();

    private readonly IReadOnlyDictionary<string, IAsmConfigUpdater> _productConfigUpdaters = new Dictionary<string, IAsmConfigUpdater> { { RcmProducts.Asm, new AsmProduct() }, { RcmProducts.AsmDd, new AsmDdProduct() }, { RcmProducts.AsmData, new AsmDataProduct() } };

    private readonly string? _embeddedRulesPath;
    private Dictionary<string, List<RemoteConfiguration>> _fileUpdates = new();
    private Dictionary<string, List<RemoteConfigurationPath>> _fileRemoves = new();

    public ConfigurationStatus(string? embeddedRulesPath) => _embeddedRulesPath = embeddedRulesPath;

    internal RuleSet? FallbackEmbeddedRuleSet { get; set; }

    internal bool? EnableAsm { get; set; } = null;

    internal string? AutoUserInstrumMode { get; set; } = null;

    internal Dictionary<string, RuleOverride[]> RulesOverridesByFile { get; } = new();

    internal Dictionary<string, RuleData[]> RulesDataByFile { get; } = new();

    internal Dictionary<string, JArray> ExclusionsByFile { get; } = new();

    internal Dictionary<string, RuleSet> RulesByFile { get; } = new();

    internal Dictionary<string, AsmFeature> AsmFeaturesByFile { get; } = new();

    internal Dictionary<string, AutoUserInstrum> AutoUserInstrumByFile { get; } = new();

    internal Dictionary<string, JArray> CustomRulesByFile { get; } = new();

    internal IncomingUpdateStatus IncomingUpdateState { get; } = new();

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

        if (IncomingUpdateState.WafKeysToApply.Contains(WafActionsKey))
        {
            dictionary.Add(WafActionsKey, Actions.Select(a => a.Value.ToKeyValuePair()).ToArray());
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

    /// <summary>
    /// Calls each product updater to deserialize all remote config payloads and store them properly in dictionaries which might involve various logical merges
    /// This method deserializes everything stored in _fileUpdates. ConfigurationStatus will have a *bigger* memory footprint.
    /// </summary>
    public void ApplyStoredFiles()
    {
        // no need to clear _fileUpdates / _fileRemoves after they've been applied, as when we receive a new config, `StoreLastConfigState` method will clear anything remaining anyway.
        foreach (var updater in _productConfigUpdaters)
        {
            var fileUpdates = _fileUpdates.TryGetValue(updater.Key, out var value);
            if (fileUpdates)
            {
                updater.Value.ProcessUpdates(this, value!);
            }

            var fileRemoves = _fileRemoves.TryGetValue(updater.Key, out var valueRemove);
            if (fileRemoves)
            {
                updater.Value.ProcessRemovals(this, valueRemove!);
            }
        }
    }

    /// <summary>
    /// This method just stores the config state without deserializing anything, this state will be ready to use and deserialized if ASM is enabled later on.
    /// This method considers that RC sends us everything again, the whole state together. That's why it's clearing all unapplied updates / removals before processing the last ones received.
    /// In case ASM remained disabled, we discard previous updates and removals stored here that were never applied.
    /// </summary>
    /// <param name="configsByProduct">configsByProduct</param>
    /// <param name="removedConfigs">removedConfigs</param>
    /// <returns>whether or not there is any change, i.e any update/removal</returns>
    public bool StoreLastConfigState(Dictionary<string, List<RemoteConfiguration>> configsByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigs)
    {
        _fileUpdates.Clear();
        _fileRemoves.Clear();
        List<RemoteConfiguration> asmFeaturesToUpdate = new();
        List<RemoteConfigurationPath> asmFeaturesToRemove = new();
        var anyChange = configsByProduct.Count > 0 || removedConfigs?.Count > 0;
        if (anyChange)
        {
            foreach (var configByProduct in configsByProduct)
            {
                if (configByProduct.Key == RcmProducts.AsmFeatures)
                {
                    asmFeaturesToUpdate.AddRange(configByProduct.Value);
                }
                else
                {
                    if (_fileUpdates.ContainsKey(configByProduct.Key))
                    {
                        _fileUpdates[configByProduct.Key].AddRange(configByProduct.Value);
                    }
                    else
                    {
                        _fileUpdates[configByProduct.Key] = configByProduct.Value;
                    }
                }
            }

            if (removedConfigs != null)
            {
                foreach (var configByProductToRemove in removedConfigs)
                {
                    if (configByProductToRemove.Key == RcmProducts.AsmFeatures)
                    {
                        asmFeaturesToRemove.AddRange(configByProductToRemove.Value);
                    }
                    else
                    {
                        if (_fileRemoves.ContainsKey(configByProductToRemove.Key))
                        {
                            _fileRemoves[configByProductToRemove.Key].AddRange(configByProductToRemove.Value);
                        }
                        else
                        {
                            _fileRemoves[configByProductToRemove.Key] = configByProductToRemove.Value;
                        }
                    }
                }

                // only treat asm_features as it will decide if asm gets toggled on and if we deserialize all the others
                // (the enable of auto user instrumentation as added to asm_features)
                _asmFeatureProduct.ProcessUpdates(this, asmFeaturesToUpdate);
                _asmFeatureProduct.ProcessRemovals(this, asmFeaturesToRemove);

                EnableAsm = !AsmFeaturesByFile.IsEmpty() && AsmFeaturesByFile.All(a => a.Value?.Enabled is null or true);

                // empty, one value, or all values the same are valid states, anything else is an error
                var autoUserInstrumMode = AutoUserInstrumByFile.Values.FirstOrDefault(x => x?.Mode is not null);
                if (autoUserInstrumMode == null ||
                    AutoUserInstrumByFile.All(x => x.Value?.Mode is null || x.Value?.Mode == autoUserInstrumMode?.Mode))
                {
                    AutoUserInstrumMode = autoUserInstrumMode?.Mode?.ToLowerInvariant();
                }
                else
                {
                    AutoUserInstrumMode = "unknown value";
                    Log.Error(
                        "AutoUserInstrumMode was 'unknown value', source data: {AutoUserInstrumByFile}",
                        string.Join(",", AutoUserInstrumByFile.Values.Select(x => x?.Mode)));
                }

                AutoUserInstrumMode = autoUserInstrumMode?.Mode?.ToLowerInvariant();
            }
        }

        return anyChange;
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
