// <copyright file="ConfigurationState.cs" company="Datadog">
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
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Action = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;

namespace Datadog.Trace.AppSec.Rcm;

/// <summary>
/// This class represents the state of RCM for ASM.
/// It has 2 possible status:
/// - ASM is not activated, and _fileUpdates/_fileRemoves contain some pending non-deserialized changes to apply when ASM_FEATURES activate ASM. Every time an RC payload is received here, pending changes are reset to the last ones
/// - ASM is activated, stored configs in _fileUpdates/_fileRemoves are applied every time.
/// </summary>
internal sealed record ConfigurationState
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConfigurationState>();

    private readonly IAsmConfigUpdater _asmFeatureProduct = new AsmFeaturesProduct();

    private readonly IReadOnlyDictionary<string, IAsmConfigUpdater> _productConfigUpdaters;

    private readonly IConfigurationTelemetry _telemetry;

    private readonly string? _rulesPath;
    private readonly bool _canBeToggled;
    private readonly Dictionary<string, List<RemoteConfiguration>> _fileUpdates = new();
    private readonly Dictionary<string, List<RemoteConfigurationPath>> _fileRemoves = new();
    private bool _defaultRulesetApplied = false;

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

    internal string? AutoUserInstrumMode { get; set; } = null;

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

    internal RemoteConfigWafFiles GetWafConfigurations(bool updating = false)
    {
        updating &= !IncomingUpdateState.ShouldInitAppsec; // If we need to init AppSec we don't want to skip any config
        var configurations = new Dictionary<string, object>();
        List<string>? removes = null;

        if (updating && _fileRemoves is { Count: > 0 })
        {
            removes = _fileRemoves.SelectMany(p => p.Value).Where(p => p.Product != RcmProducts.AsmFeatures).Select(v => v.Path).ToList();
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

        return new(configurations.Count > 0 ? configurations : null, removes);

        bool IsNewUpdate(string path)
        {
            foreach (var productUpdates in _fileUpdates)
            {
                if (productUpdates.Value.Any(u => u.Path.Path == path))
                {
                    return true;
                }
            }

            return false;
        }
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
            var fileRemoves = _fileRemoves.TryGetValue(updater.Key, out var removes);
            var fileUpdates = _fileUpdates.TryGetValue(updater.Key, out var updates);
            if (fileRemoves || fileUpdates)
            {
                updater.Value.ProcessUpdates(this, removes, updates);
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
        // only deserialize and apply asm_features as it will decide if asm gets toggled on and if we deserialize all the others
        // (the enable of auto user instrumentation as added to asm_features)
        var change = _fileRemoves.TryGetValue(RcmProducts.AsmFeatures, out var removals);
        change |= _fileUpdates.TryGetValue(RcmProducts.AsmFeatures, out var updates);

        if (change)
        {
            _asmFeatureProduct.ProcessUpdates(this, removals, updates);
        }

        if (!change) { return; }

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

    internal sealed record IncomingUpdateStatus : IDisposable
    {
        internal bool ShouldInitAppsec { get; set; } = false;

        internal bool ShouldUpdateAppsec { get; set; } = false;

        internal bool ShouldDisableAppsec { get; set; } = false;

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
