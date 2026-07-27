// <copyright file="ConfigurationState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.AppSec.Rcm.Models.AsmDd;
using Datadog.Trace.AppSec.Rcm.Models.AsmFeatures;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Action = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;

namespace Datadog.Trace.AppSec.Rcm;

/// <summary>
/// This class represents the state of RCM for ASM. `_pendingByProduct` accumulates RCM operations
/// (updates and removes) keyed by config path, with "latest op wins" semantics. RCM only forwards
/// deltas, so configs received while ASM is disabled must persist across polls — otherwise they'd
/// be silently dropped when ASM finally activates.
///
/// Each product slot is drained by its last consumer:
/// - ASM_FEATURES is drained inside `ApplyAsmFeatures` (the FEATURES updater is its only consumer).
/// - ASM_DD / ASM / ASM_DATA are drained at the end of `BuildWafUpdatePayload`, which is the last
///   reader of those slots in the cycle. When ASM is disabled and that method isn't called, the
///   WAF-relevant slots persist across polls (which is what enables the accumulator behavior).
/// </summary>
internal sealed record ConfigurationState
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConfigurationState>();

    private readonly AsmFeaturesProduct _asmFeatureProduct = new AsmFeaturesProduct();

    private readonly IReadOnlyDictionary<string, IAsmConfigUpdater> _productConfigUpdaters;

    private readonly IConfigurationTelemetry _telemetry;

    private readonly string? _rulesPath;
    private readonly bool _canBeToggled;

    // Pending RCM operations grouped by product. Outer key is product, inner key is the full config path.
    // Each path can hold one pending op (later update or remove overwrites whatever was there) — the RCM
    // "latest delta wins" semantics. Drained per-product by the last consumer: ASM_FEATURES inside
    // ApplyAsmFeatures, WAF-relevant products at the end of BuildWafUpdatePayload.
    private readonly Dictionary<string, Dictionary<string, PendingOperation>> _pendingByProduct = new();

    private bool _defaultRulesetApplied;

    public ConfigurationState(SecuritySettings settings, IConfigurationTelemetry telemetry, bool wafIsNull)
    {
        _telemetry = telemetry;
        _productConfigUpdaters = new Dictionary<string, IAsmConfigUpdater>
        {
            { RcmProducts.AsmDd, new AsmDdProduct() },
            { RcmProducts.Asm, new AsmGenericProduct(() => AsmConfigs) },
            { RcmProducts.AsmData, new AsmGenericProduct(() => AsmDataConfigs) }
        };

        _rulesPath = settings.Rules;
        _canBeToggled = settings.CanBeToggled;
        if (settings.AppsecEnabled && wafIsNull)
        {
            IncomingUpdateState.ShouldInitAppsec = true;
        }
    }

    public ConfigurationState(SecuritySettings settings, IConfigurationTelemetry telemetry, bool wafIsNull, List<KeyValuePair<string, RuleSet>>? rulesetConfigs, Dictionary<string, Models.Asm.Payload>? asmConfigs, Dictionary<string, Models.AsmData.Payload>? asmDataConfigs)
        : this(settings, telemetry, wafIsNull)
    {
        if (rulesetConfigs is not null)
        {
            RulesetConfigs = rulesetConfigs;
        }

        if (asmConfigs is not null)
        {
            AsmConfigs = asmConfigs.ToDictionary(p => p.Key, p => JToken.FromObject(p.Value));
        }

        if (asmDataConfigs is not null)
        {
            AsmDataConfigs = asmDataConfigs.ToDictionary(p => p.Key, p => JToken.FromObject(p.Value));
        }
    }

    public bool AppsecEnabled { get; set; }

    internal string? AutoUserInstrumMode { get; set; }

    // RC Product: ASM_FEATURES
    internal Dictionary<string, AsmFeature> AsmFeaturesByFile { get; } = new();

    internal Dictionary<string, AutoUserInstrum> AutoUserInstrumByFile { get; } = new();

    // RC Product: ASM
    internal Dictionary<string, JToken> AsmConfigs { get; } = new();

    // RC Product: ASM_DATA
    internal Dictionary<string, JToken> AsmDataConfigs { get; } = new();

    // RC Product: ASM_DD
    internal List<KeyValuePair<string, RuleSet>> RulesetConfigs { get; } = new();

    internal IncomingUpdateStatus IncomingUpdateState { get; } = new();

    public string? RulesPath => _rulesPath;

    public bool HasRemoteConfig { get; private set; }

    public string? RuleSetTitle => HasRemoteConfig ? "RemoteConfig" : _rulesPath;

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

    /// <summary>
    /// Builds the payload to push to the WAF (updates + removes) by reading pending RCM operations
    /// and the deserialized destination dictionaries.
    /// </summary>
    /// <remarks>
    /// This method is the last consumer of WAF-relevant pending entries in an RCM cycle, so it
    /// DRAINS the ASM_DD / ASM / ASM_DATA slots of <see cref="_pendingByProduct"/> on its way out.
    /// Calling it twice in the same cycle will return an empty delta the second time.
    /// </remarks>
    internal RemoteConfigWafFiles BuildWafUpdatePayload(bool updating = false)
    {
        updating &= !IncomingUpdateState.ShouldInitAppsec; // If we need to init AppSec we don't want to skip any config
        var configurations = new Dictionary<string, object>();
        List<string>? removes = null;

        if (updating && _pendingByProduct.Count > 0)
        {
            removes = _pendingByProduct
                     .Where(entry => entry.Key != RcmProducts.AsmFeatures)
                     .SelectMany(p => p.Value.Values)
                     .Where(op => op.IsRemove)
                     .Select(v => v.Path.Path)
                     .ToList();
        }

        if (AsmConfigs is { Count: > 0 })
        {
            foreach (var config in AsmConfigs)
            {
                if (updating && !IsNewUpdate(config.Key)) { continue; }
                configurations[config.Key] = config.Value;
            }
        }

        if (AsmDataConfigs is { Count: > 0 })
        {
            foreach (var config in AsmDataConfigs)
            {
                if (updating && !IsNewUpdate(config.Key)) { continue; }
                configurations[config.Key] = config.Value;
            }
        }

        // if there's incoming rules or empty rules, or if asm is to be activated, we also want the rules key in waf arguments
        if (!_defaultRulesetApplied && RulesetConfigs.Count == 0)
        {
            // Deserialize from LocalRuleFile
            var deserializedFromLocalRules = WafConfigurator.DeserializeEmbeddedOrStaticRules(RulesPath);
            if (deserializedFromLocalRules is not null)
            {
                configurations[AsmDdProduct.DefaultConfigKey] = deserializedFromLocalRules;
                _defaultRulesetApplied = true;
            }
        }
        else if (RulesetConfigs.Count > 0 && string.IsNullOrEmpty(RulesPath))
        {
            // Use incoming RC rules if no external rules path has been defined
            foreach (var config in RulesetConfigs)
            {
                if (updating && !IsNewUpdate(config.Key)) { continue; }

                var configuration = new Dictionary<string, object>();
                config.Value?.AddToDictionaryAtRoot(configuration);
                configurations[config.Key] = configuration;
            }

            if (_defaultRulesetApplied)
            {
                if (removes is null) { removes = [AsmDdProduct.DefaultConfigKey]; }
                else { removes.Add(AsmDdProduct.DefaultConfigKey); }
                _defaultRulesetApplied = false;
            }
        }

        // Drain WAF-relevant pending slots. ASM_FEATURES is handled separately in ApplyAsmFeatures.
        foreach (var entry in _pendingByProduct)
        {
            if (entry.Key == RcmProducts.AsmFeatures) { continue; }
            entry.Value.Clear();
        }

        return new(configurations.Count > 0 ? configurations : null, removes);

        // True if `path` has a pending update (not a remove) in any WAF-relevant product slot.
        bool IsNewUpdate(string path)
        {
            foreach (var entry in _pendingByProduct)
            {
                if (entry.Key == RcmProducts.AsmFeatures) { continue; }
                if (entry.Value.TryGetValue(path, out var op) && !op.IsRemove)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Hands each product's pending batch to its updater, which deserializes and merges into the destination
    /// dictionaries (RulesetConfigs / AsmConfigs / AsmDataConfigs). Pending entries are NOT cleared here —
    /// <see cref="BuildWafUpdatePayload"/> still needs to read them to know what's new in this cycle's delta,
    /// and is responsible for draining them on the way out.
    /// </summary>
    public void ApplyStoredFiles()
    {
        foreach (var updater in _productConfigUpdaters)
        {
            if (_pendingByProduct.TryGetValue(updater.Key, out var pending) && pending.Count != 0)
            {
                SplitPending(pending, out var removes, out var updates);
                updater.Value.ProcessUpdates(this, removes, updates);
            }
        }
    }

    /// <summary>
    /// Merges an incoming RCM delta into the per-product pending state without deserializing payloads.
    /// If ASM is enabled (or this delta turns it on), the merged state is applied immediately; otherwise it stays pending until ASM activates.
    /// </summary>
    /// <remarks>
    /// Background: RCM forwards only the DELTA versus what we last acknowledged — not the full backend state.
    /// A config received in an earlier poll is NOT re-sent on the next poll if it hasn't changed.
    /// So while ASM is disabled, we must accumulate deltas across polls, otherwise configs received
    /// before activation would be silently dropped (they're already in RCM's _appliedConfigurations
    /// and won't be re-forwarded). Each path holds exactly one pending op in _pendingByProduct;
    /// a later update or remove for the same path overwrites whatever was there.
    /// </remarks>
    public void ReceivedNewConfig(Dictionary<string, List<RemoteConfiguration>> configsByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigs)
    {
        var anyChange = configsByProduct.Count > 0 || removedConfigs?.Count > 0;
        if (anyChange)
        {
            // Tracks whether this delta touches any product OTHER than ASM_FEATURES. A pure ASM_FEATURES
            // delta might only toggle AppSec on/off, it doesn't carry WAF rules, so when that's all we get,
            // there's no reason to push a config update through to the WAF below.
            var hasUpdateConfigurations = false;

            if (removedConfigs is not null)
            {
                foreach (var entry in removedConfigs)
                {
                    var productKey = entry.Key;
                    if (productKey != RcmProducts.AsmFeatures)
                    {
                        hasUpdateConfigurations = true;
                    }

                    if (!_pendingByProduct.TryGetValue(productKey, out var pending))
                    {
                        pending = new Dictionary<string, PendingOperation>();
                        _pendingByProduct[productKey] = pending;
                    }

                    foreach (var removed in entry.Value)
                    {
                        // Overwrites any pending update OR pending remove for this path. Later op wins.
                        pending[removed.Path] = new PendingOperation(removed, update: null);
                    }
                }
            }

            foreach (var entry in configsByProduct)
            {
                var productKey = entry.Key;
                if (productKey != RcmProducts.AsmFeatures)
                {
                    hasUpdateConfigurations = true;
                }

                if (!_pendingByProduct.TryGetValue(productKey, out var pending))
                {
                    pending = new Dictionary<string, PendingOperation>();
                    _pendingByProduct[productKey] = pending;
                }

                foreach (var update in entry.Value)
                {
                    // Same overwrite semantics — replaces any prior op for this path.
                    pending[update.Path.Path] = new PendingOperation(update.Path, update);
                }
            }

            // ASM_FEATURES is processed eagerly because it can flip AppsecEnabled and decide whether
            // we initialize the WAF, update an already-running WAF, or do nothing.
            ApplyAsmFeatures(AppsecEnabled);
            IncomingUpdateState.ShouldUpdateAppsec = !IncomingUpdateState.ShouldInitAppsec && AppsecEnabled && hasUpdateConfigurations;

            // If ASM just turned on, or it was already on and we got real WAF config, deserialize pending
            // state into the per-product output dictionaries (RulesetConfigs, AsmConfigs, AsmDataConfigs).
            // The pending entries themselves stay around until BuildWafUpdatePayload drains them.
            if (IncomingUpdateState.ShouldUpdateAppsec || IncomingUpdateState.ShouldInitAppsec)
            {
                ApplyStoredFiles();
            }
        }
    }

    private void ApplyAsmFeatures(bool appsecCurrentlyEnabled)
    {
        // only deserialize and apply asm_features as it will decide if asm gets toggled on and if we deserialize all the others
        // (the enable of auto user instrumentation as added to asm_features)
        if (!_pendingByProduct.TryGetValue(RcmProducts.AsmFeatures, out var pending) || pending.Count == 0)
        {
            return;
        }

        SplitPending(pending, out var removals, out var updates);
        _asmFeatureProduct.ProcessUpdates(this, removals, updates);
        pending.Clear();

        // normally CanBeToggled should not need a check as asm_features capacity is only sent if AppSec env var is null, but still guards it in case
        if (_canBeToggled)
        {
            var rcmEnable = !AsmFeaturesByFile.IsEmpty() && !AsmFeaturesByFile.Any(a => a.Value?.Enabled is false);
            if (!appsecCurrentlyEnabled)
            {
                IncomingUpdateState.ShouldInitAppsec = rcmEnable;
                _defaultRulesetApplied = false;
            }
            else if (!rcmEnable)
            {
                IncomingUpdateState.ShouldDisableAppsec = true;
            }

            // Update AppsecEnabled state based on the RCM data on Telemetry Config
            if (IncomingUpdateState.ShouldInitAppsec)
            {
                _telemetry.Record(ConfigurationKeys.AppSec.Enabled, true, ConfigurationOrigins.RemoteConfig);
            }
            else if (IncomingUpdateState.ShouldDisableAppsec)
            {
                _telemetry.Record(ConfigurationKeys.AppSec.Enabled, false, ConfigurationOrigins.RemoteConfig);
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

    // Splits the per-product pending map into the (removes, updates) shape that IAsmConfigUpdater.ProcessUpdates expects.
    // Either out parameter may be null when that side is empty — matching the interface contract.
    private static void SplitPending(Dictionary<string, PendingOperation> pending, out List<RemoteConfigurationPath>? removes, out List<RemoteConfiguration>? updates)
    {
        removes = null;
        updates = null;
        foreach (var op in pending.Values)
        {
            if (op.IsRemove)
            {
                removes ??= [];
                removes.Add(op.Path);
            }
            else
            {
                updates ??= [];
                updates.Add(op.Update);
            }
        }
    }

    // A pending RCM operation for one config path: either an update carrying a fresh payload,
    // or a removal marker. The Update field is null iff this entry represents a removal.
    private readonly struct PendingOperation(RemoteConfigurationPath path, RemoteConfiguration? update)
    {
        public RemoteConfigurationPath Path { get; } = path;

        public RemoteConfiguration? Update { get; } = update;

        [MemberNotNullWhen(false, nameof(Update))]
        public bool IsRemove => Update is null;
    }

    internal sealed record IncomingUpdateStatus : IDisposable
    {
        internal bool ShouldInitAppsec { get; set; }

        internal bool ShouldUpdateAppsec { get; set; }

        internal bool ShouldDisableAppsec { get; set; }

        public void Dispose() => Reset();

        public void Reset()
        {
            ShouldDisableAppsec = false;
            ShouldInitAppsec = false;
            ShouldUpdateAppsec = false;
        }
    }

    internal sealed record RemoteConfigWafFiles(Dictionary<string, object>? Updates, List<string>? Removes)
    {
        public bool HasData => Updates is not null || Removes is not null;
    }
}
