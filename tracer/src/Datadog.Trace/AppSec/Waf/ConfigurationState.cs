// <copyright file="ConfigurationState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.AppSec.Rcm.Models.AsmDd;
using Datadog.Trace.AppSec.Rcm.Models.AsmFeatures;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.Initialization;
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
internal record ConfigurationState
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConfigurationState>();

    internal const string WafRulesKey = "rules";
    internal const string WafRulesOverridesKey = "rules_override";
    internal const string WafExclusionsKey = "exclusions";
    internal const string WafRulesDataKey = "rules_data";
    internal const string WafExclusionsDataKey = "exclusion_data";
    internal const string WafCustomRulesKey = "custom_rules";
    internal const string WafActionsKey = "actions";
    private readonly IAsmConfigUpdater _asmFeatureProduct = new AsmFeaturesProduct();

    private readonly IReadOnlyDictionary<string, IAsmConfigUpdater> _productConfigUpdaters = new Dictionary<string, IAsmConfigUpdater> { { RcmProducts.Asm, new AsmProduct() }, { RcmProducts.AsmDd, new AsmDdProduct() }, { RcmProducts.AsmData, new AsmDataProduct() } };

    private readonly string? _rulesPath;
    private readonly bool _canBeToggled;
    private readonly Dictionary<string, List<RemoteConfiguration>> _fileUpdates = new();
    private readonly Dictionary<string, List<RemoteConfigurationPath>> _fileRemoves = new();

    public ConfigurationState(SecuritySettings settings, bool wafIsNull)
    {
        _rulesPath = settings.Rules;
        _canBeToggled = settings.CanBeToggled;
        if (settings.AppsecEnabled && wafIsNull)
        {
            IncomingUpdateState.ShouldInitAppsec = true;
        }

        RefreshState();
    }

    public ConfigurationState(SecuritySettings settings, bool wafIsNull, Dictionary<string, RuleSet>? rulesByFile, Dictionary<string, RuleData[]>? ruleDataByFile, Dictionary<string, RuleOverride[]>? ruleOverrideByFile, Dictionary<string, Action[]>? actionsByFile = null)
        : this(settings, wafIsNull)
    {
        if (rulesByFile is not null)
        {
            RulesByFile = rulesByFile;
            IncomingUpdateState.WafKeysToApply.Add(WafRulesKey);
        }

        if (ruleDataByFile is not null)
        {
            RulesDataByFile = ruleDataByFile;
            IncomingUpdateState.WafKeysToApply.Add(WafRulesDataKey);
        }

        if (ruleOverrideByFile is not null)
        {
            RulesOverridesByFile = ruleOverrideByFile;
            IncomingUpdateState.WafKeysToApply.Add(WafRulesOverridesKey);
        }

        if (actionsByFile is not null)
        {
            ActionsByFile = actionsByFile;
            IncomingUpdateState.WafKeysToApply.Add(WafActionsKey);
        }

        RefreshState();
    }

    public bool AppsecEnabled { get; set; }

    internal string? AutoUserInstrumMode { get; set; } = null;

    internal Dictionary<string, RuleOverride[]> RulesOverridesByFile { get; } = new();

    internal Dictionary<string, RuleData[]> RulesDataByFile { get; } = new();

    internal Dictionary<string, RuleData[]> ExclusionsDataByFile { get; } = new();

    internal Dictionary<string, JArray> ExclusionsByFile { get; } = new();

    internal Dictionary<string, RuleSet> RulesByFile { get; } = new();

    internal Dictionary<string, AsmFeature> AsmFeaturesByFile { get; } = new();

    internal Dictionary<string, AutoUserInstrum> AutoUserInstrumByFile { get; } = new();

    internal Dictionary<string, JArray> CustomRulesByFile { get; } = new();

    internal Dictionary<string, Action[]> ActionsByFile { get; init; } = new();

    internal IncomingUpdateStatus IncomingUpdateState { get; } = new();

    internal BigInteger State { get; private set; }

    public string? RulesPath => _rulesPath;

    public bool HasRemoteConfig { get; private set; }

    public string? RuleSetTitle => HasRemoteConfig ? "RemoteConfig" : _rulesPath;

    private void RefreshState()
    {
        SetCapability(StateIndices.AppsecCanBeSwitched, _canBeToggled);

        void SetCapability(BigInteger index, bool available)
        {
            if (available)
            {
                State |= index;
            }
            else
            {
                State &= ~index;
            }
        }
    }

    internal string[] WhatProductsAreRelevant(SecuritySettings settings)
    {
        var subscriptionsKeys = new List<string>();
        var canBeToggledOrAppSecEnabled = settings.CanBeToggled || settings.AppsecEnabled;
        if (canBeToggledOrAppSecEnabled)
        {
            subscriptionsKeys.Add(RcmProducts.AsmFeatures);

            if (settings.NoCustomLocalRules)
            {
                subscriptionsKeys.Add(RcmProducts.AsmDd);
            }

            if (AppsecEnabled)
            {
                subscriptionsKeys.AddRange([RcmProducts.Asm, RcmProducts.AsmData]);
            }
        }

        return [.. subscriptionsKeys];
    }

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

    internal object? BuildDictionaryForWafAccordingToIncomingUpdate()
    {
        var configuration = new Dictionary<string, object>();

        if (IncomingUpdateState.WafKeysToApply.Contains(WafExclusionsKey))
        {
            var exclusions = ExclusionsByFile.SelectMany(x => x.Value).ToList();
            configuration.Add(WafExclusionsKey, new JArray(exclusions));
        }

        if (IncomingUpdateState.WafKeysToApply.Contains(WafRulesOverridesKey))
        {
            var overrides = RulesOverridesByFile.SelectMany(x => x.Value).ToList();
            configuration.Add(WafRulesOverridesKey, overrides.Select(r => r.ToKeyValuePair()).ToArray());
        }

        if (IncomingUpdateState.WafKeysToApply.Contains(WafRulesDataKey))
        {
            var rulesData = MergeRuleData(RulesDataByFile.SelectMany(x => x.Value));
            configuration.Add(WafRulesDataKey, rulesData.Select(r => r.ToKeyValuePair()).ToArray());
        }

        if (IncomingUpdateState.WafKeysToApply.Contains(WafExclusionsDataKey))
        {
            var rulesData = MergeRuleData(ExclusionsDataByFile.SelectMany(x => x.Value));
            configuration.Add(WafExclusionsDataKey, rulesData.Select(r => r.ToKeyValuePair()).ToArray());
        }

        if (IncomingUpdateState.WafKeysToApply.Contains(WafActionsKey))
        {
            var actions = ActionsByFile.SelectMany(x => x.Value).ToList();
            configuration.Add(WafActionsKey, actions.Select(r => r.ToKeyValuePair()).ToArray());
        }

        if (IncomingUpdateState.WafKeysToApply.Contains(WafCustomRulesKey))
        {
            var customRules = CustomRulesByFile.SelectMany(x => x.Value).ToList();
            var mergedCustomRules = new JArray(customRules);
            configuration.Add(WafCustomRulesKey, mergedCustomRules);
        }

        // if there's incoming rules or empty rules, or if asm is to be activated, we also want the rules key in waf arguments
        if (IncomingUpdateState.WafKeysToApply.Contains(WafRulesKey) || IncomingUpdateState.ShouldInitAppsec)
        {
            var rulesetFromRcm = RulesByFile.Values.FirstOrDefault();
            // should deserialize from LocalRuleFile
            if (rulesetFromRcm is null)
            {
                var deserializedFromLocalRules = WafConfigurator.DeserializeEmbeddedOrStaticRules(RulesPath);
                if (deserializedFromLocalRules is not null)
                {
                    if (configuration.Count == 0)
                    {
                        return deserializedFromLocalRules;
                    }

                    var ruleSet = RuleSet.From(deserializedFromLocalRules);
                    ruleSet.AddToDictionaryAtRoot(configuration);
                }
            }
            else
            {
                rulesetFromRcm?.AddToDictionaryAtRoot(configuration);
            }
        }

        return configuration.Count > 0 ? configuration : null;
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
    public void ReceivedNewConfig(Dictionary<string, List<RemoteConfiguration>> configsByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigs)
    {
        _fileUpdates.Clear();
        _fileRemoves.Clear();
        // if we just have asm features, it might only be to toggle appsec. Other products bring actual configurations to the waf.
        var hasUpdateConfigurations = false;
        var anyChange = configsByProduct.Count > 0 || removedConfigs?.Count > 0;
        if (anyChange)
        {
            foreach (var configByProduct in configsByProduct)
            {
                if (configByProduct.Key != RcmProducts.AsmFeatures)
                {
                    hasUpdateConfigurations = true;
                }

                if (_fileUpdates.ContainsKey(configByProduct.Key))
                {
                    _fileUpdates[configByProduct.Key].AddRange(configByProduct.Value);
                }
                else
                {
                    _fileUpdates[configByProduct.Key] = configByProduct.Value;
                }
            }

            if (removedConfigs != null)
            {
                foreach (var configByProductToRemove in removedConfigs)
                {
                    if (configByProductToRemove.Key != RcmProducts.AsmFeatures)
                    {
                        hasUpdateConfigurations = true;
                    }

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

            ApplyAsmFeatures(AppsecEnabled);
            IncomingUpdateState.ShouldUpdateAppsec = !IncomingUpdateState.ShouldInitAppsec && AppsecEnabled && hasUpdateConfigurations;

            if (IncomingUpdateState.ShouldUpdateAppsec || IncomingUpdateState.ShouldInitAppsec)
            {
                ApplyStoredFiles();
            }
        }
    }

    private void ApplyAsmFeatures(bool appsecCurrentlyEnabled)
    {
        var change = false;
        // only deserialize and apply asm_features as it will decide if asm gets toggled on and if we deserialize all the others
        // (the enable of auto user instrumentation as added to asm_features)
        if (_fileUpdates.TryGetValue(RcmProducts.AsmFeatures, out var updates))
        {
            _asmFeatureProduct.ProcessUpdates(this, updates);
            change = true;
        }

        if (_fileRemoves.TryGetValue(RcmProducts.AsmFeatures, out var removals))
        {
            _asmFeatureProduct.ProcessRemovals(this, removals);
            change = true;
        }

        if (!change) { return; }

        // normally CanBeToggled should not need a check as asm_features capacity is only sent if AppSec env var is null, but still guards it in case
        if (_canBeToggled)
        {
            var rcmEnable = !AsmFeaturesByFile.IsEmpty() && !AsmFeaturesByFile.Any(a => a.Value?.Enabled is false);
            if (!appsecCurrentlyEnabled)
            {
                IncomingUpdateState.ShouldInitAppsec = rcmEnable;
            }
            else if (!rcmEnable)
            {
                IncomingUpdateState.ShouldDisableAppsec = true;
            }
        }

        // empty, one value, or all values the same are valid states, anything else is an error
        var autoUserInstrumMode = AutoUserInstrumByFile.Values.FirstOrDefault(x => x?.Mode is not null);
        if (autoUserInstrumMode is not null && AutoUserInstrumByFile.Any(x => x.Value?.Mode is not null && x.Value.Mode != autoUserInstrumMode.Mode))
        {
            AutoUserInstrumMode = "unknown value";
            Log.Error(
                "AutoUserInstrumMode was 'unknown value', source data: {AutoUserInstrumByFile}",
                string.Join(",", AutoUserInstrumByFile.Values.Select(x => x?.Mode)));
        }
        else
        {
            AutoUserInstrumMode = autoUserInstrumMode?.Mode?.ToLowerInvariant();
        }
    }

    internal record IncomingUpdateStatus : IDisposable
    {
        internal bool ShouldInitAppsec { get; set; } = false;

        internal bool ShouldUpdateAppsec { get; set; } = false;

        internal bool ShouldDisableAppsec { get; set; } = false;

        internal HashSet<string> WafKeysToApply { get; } = new();

        public void Dispose() => Reset();

        public void Reset()
        {
            WafKeysToApply.Clear();
            ShouldDisableAppsec = false;
            ShouldInitAppsec = false;
            ShouldUpdateAppsec = false;
        }
    }
}
