// <copyright file="Security.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Datadog.Trace.AppSec.RcmModels;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.AppSec
{
    /// <summary>
    /// The Secure is responsible coordinating ASM
    /// </summary>
    internal class Security : IDatadogSecurity, IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Security>();

        private static Security _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();
        private readonly SecuritySettings _settings;
        private readonly RemoteConfigurationStatus _remoteConfigurationStatus = new();
        private LibraryInitializationResult _libraryInitializationResult;
        private IWaf _waf;
        private WafLibraryInvoker _wafLibraryInvoker;
        private AppSecRateLimiter _rateLimiter;
        private bool _enabled = false;
        private InitResult _wafInitResult;

        static Security()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Security"/> class with default settings.
        /// </summary>
        public Security(SecuritySettings settings = null, IWaf waf = null, IDictionary<string, RcmModels.Asm.Action> actions = null)
            : this(settings, waf) => _remoteConfigurationStatus.Actions = actions;

        private Security(SecuritySettings settings = null, IWaf waf = null)
        {
            try
            {
                _settings = settings ?? SecuritySettings.FromDefaultSources();
                _waf = waf;
                LifetimeManager.Instance.AddShutdownTask(RunShutdown);

                if (_settings.CanBeEnabled)
                {
                    if (_settings.Enabled && _waf == null)
                    {
                        InitWafAndInstrumentations();
                    }

                    AsmRemoteConfigurationProducts.AsmFeaturesProduct.ConfigChanged += FeaturesProductConfigChanged;
                    AsmRemoteConfigurationProducts.AsmDDProduct.ConfigChanged += AsmDDProductConfigChanged;
                }
                else
                {
                    Log.Information("AppSec remote enabling not allowed (DD_APPSEC_ENABLED=false).");
                }

                SetRemoteConfigCapabilites();
            }
            catch (Exception ex)
            {
                _settings = new(source: null) { Enabled = false };
                Log.Error(ex, "DDAS-0001-01: AppSec could not start because of an unexpected error. No security activities will be collected. Please contact support at https://docs.datadoghq.com/help/ for help.");
            }
        }

        /// <summary>
        /// Gets or sets the global <see cref="Security"/> instance.
        /// </summary>
        public static Security Instance
        {
            get => LazyInitializer.EnsureInitialized(ref _instance, ref _globalInstanceInitialized, ref _globalInstanceLock, () => new Security(null, null));

            set
            {
                lock (_globalInstanceLock)
                {
                    _instance = value;
                    _globalInstanceInitialized = true;
                }
            }
        }

        internal bool WafExportsErrorHappened => _libraryInitializationResult?.ExportErrorHappened ?? false;

        internal string WafRuleFileVersion { get; private set; }

        internal InitResult WafInitResult => _wafInitResult;

        /// <summary>
        /// Gets <see cref="SecuritySettings"/> instance
        /// </summary>
        SecuritySettings IDatadogSecurity.Settings => _settings;

        internal SecuritySettings Settings => _settings;

        internal string DdlibWafVersion => _waf?.Version;

        internal BlockingAction GetBlockingAction(string id, string[] requestAcceptHeaders)
        {
            var blockingAction = new BlockingAction();
            RcmModels.Asm.Action action = null;
            _remoteConfigurationStatus.Actions?.TryGetValue(id, out action);

            void SetAutomaticResponseContent()
            {
                if (requestAcceptHeaders != null)
                {
                    foreach (var value in requestAcceptHeaders)
                    {
                        if (value?.Contains(AspNet.MimeTypes.Json) ?? false)
                        {
                            SetJsonResponseContent();
                            break;
                        }

                        if (value?.Contains(AspNet.MimeTypes.TextHtml) ?? false)
                        {
                            SetHtmlResponseContent();
                        }
                    }
                }

                if (blockingAction.ContentType == null)
                {
                    SetJsonResponseContent();
                }
            }

            void SetJsonResponseContent()
            {
                blockingAction.ContentType = AspNet.MimeTypes.Json;
                blockingAction.ResponseContent = _settings.BlockedJsonTemplate;
            }

            void SetHtmlResponseContent()
            {
                blockingAction.ContentType = AspNet.MimeTypes.TextHtml;
                blockingAction.ResponseContent = _settings.BlockedHtmlTemplate;
            }

            if (action?.Parameters == null)
            {
                SetAutomaticResponseContent();
                blockingAction.StatusCode = 403;
            }
            else
            {
                if (action.Type == BlockingAction.BlockRequestType)
                {
                    switch (action.Parameters!.Type)
                    {
                        case "auto":
                            SetAutomaticResponseContent();
                            break;

                        case "json":
                            SetJsonResponseContent();
                            break;

                        case "html":
                            SetHtmlResponseContent();
                            break;
                    }

                    blockingAction.StatusCode = action.Parameters.StatusCode;
                }
                else if (action.Type == BlockingAction.RedirectRequestType)
                {
                    if (!string.IsNullOrEmpty(action.Parameters.Location))
                    {
                        blockingAction.StatusCode = action.Parameters.StatusCode is >= 300 and < 400 ? action.Parameters.StatusCode : 303;
                        blockingAction.RedirectLocation = action.Parameters.Location;
                        blockingAction.IsRedirect = true;
                    }
                    else
                    {
                        Log.Warning("Received a custom block action of type redirect with a status code {StatusCode}, an automatic response will be set", action.Parameters.StatusCode.ToString());
                        SetAutomaticResponseContent();
                        blockingAction.StatusCode = 403;
                    }
                }
            }

            return blockingAction;
        }

        private static void AddAppsecSpecificInstrumentations()
        {
            int defs = 0, derived = 0;
            try
            {
                Log.Debug("Adding CallTarget AppSec integration definitions to native library.");
                var payload = InstrumentationDefinitions.GetAllDefinitions(InstrumentationCategory.AppSec);
                NativeMethods.InitializeProfiler(payload.DefinitionsId, payload.Definitions);
                defs = payload.Definitions.Length;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding CallTarget AppSec integration definitions to native library");
            }

            try
            {
                Log.Debug("Adding CallTarget appsec derived integration definitions to native library.");
                var payload = InstrumentationDefinitions.GetDerivedDefinitions(InstrumentationCategory.AppSec);
                NativeMethods.InitializeProfiler(payload.DefinitionsId, payload.Definitions);
                derived = payload.Definitions.Length;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding CallTarget appsec derived integration definitions to native library");
            }

            Log.Information<int, int>("{DefinitionCount} AppSec definitions and {DerivedCount} AppSec derived definitions added to the profiler.", defs, derived);
        }

        private static void RemoveAppsecSpecificInstrumentations()
        {
            int defs = 0, derived = 0;
            try
            {
                Log.Debug("Removing CallTarget AppSec integration definitions from native library.");
                var payload = InstrumentationDefinitions.GetAllDefinitions(InstrumentationCategory.AppSec);
                NativeMethods.RemoveCallTargetDefinitions(payload.DefinitionsId, payload.Definitions);
                defs = payload.Definitions.Length;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error removing CallTarget AppSec integration definitions from native library");
            }

            try
            {
                Log.Debug("Removing CallTarget appsec derived integration definitions from native library.");
                var payload = InstrumentationDefinitions.GetDerivedDefinitions(InstrumentationCategory.AppSec);
                NativeMethods.RemoveCallTargetDefinitions(payload.DefinitionsId, payload.Definitions);
                derived = payload.Definitions.Length;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error removing CallTarget appsec derived integration definitions from native library");
            }

            Log.Information<int, int>("{DefinitionCount} AppSec definitions and {DerivedCount} AppSec derived definitions removed from the profiler.", defs, derived);
        }

        /// <summary> Frees resources </summary>
        public void Dispose() => _waf?.Dispose();

        private void SetRemoteConfigCapabilites()
        {
            RemoteConfigurationManager.CallbackWithInitializedInstance(
                rcm =>
                {
                    var noLocalRules = _settings.Rules == null;
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmActivation, _settings.CanBeEnabled);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmDdRules, noLocalRules);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmIpBlocking, noLocalRules);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmExclusion, noLocalRules);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmRequestBlocking, noLocalRules);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmResponseBlocking, noLocalRules);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmCustomBlockingResponse, noLocalRules);
                });
        }

        private void AsmDDProductConfigChanged(object sender, ProductConfigChangedEventArgs e)
        {
            var asmDd = e.GetConfigurationAsString().FirstOrDefault();
            if (!string.IsNullOrEmpty(asmDd.TypedFile))
            {
                _remoteConfigurationStatus.RemoteRulesJson = asmDd.TypedFile;
                if (_enabled)
                {
                    var result = _waf?.UpdateRules(_remoteConfigurationStatus.RemoteRulesJson);
                    WafRuleFileVersion = result?.RuleFileVersion;
                    if (_wafInitResult?.Success ?? false)
                    {
                        e.Acknowledge(asmDd.Name);
                    }
                    else
                    {
                        e.Error(asmDd.Name, "An error happened updating waf rules");
                    }

                    return;
                }
            }

            e.Acknowledge(asmDd.Name);
        }

        private void FeaturesProductConfigChanged(object sender, ProductConfigChangedEventArgs e)
        {
            var features = e.GetDeserializedConfigurations<AsmFeatures>().FirstOrDefault();
            if (features.TypedFile != null)
            {
                _settings.Enabled = features.TypedFile.Asm.Enabled;
                if (_settings.Enabled)
                {
                    InitWafAndInstrumentations(true);
                }
                else
                {
                    DisposeWafAndInstrumentations(true);
                }
            }

            e.Acknowledge(features.Name);
        }

        private void AsmDataProductConfigRemoved(object sender, ProductConfigChangedEventArgs e)
        {
            var asmDataConfigs = e.GetDeserializedConfigurations<RcmModels.AsmData.Payload>();
            foreach (var asmDataConfig in asmDataConfigs)
            {
                if (_remoteConfigurationStatus.RulesDataByFile.ContainsKey(asmDataConfig.Name))
                {
                    _remoteConfigurationStatus.RulesDataByFile.Remove(asmDataConfig.Name);
                }
            }

            var updated = true;
            if (_enabled)
            {
                var ruleData = _remoteConfigurationStatus.RulesDataByFile.SelectMany(x => x.Value).ToList();
                updated = UpdateWafWithRulesData(ruleData);
            }

            foreach (var asmDataConfig in asmDataConfigs)
            {
                if (!updated)
                {
                    e.Error(asmDataConfig.Name, "Waf could not remove the rules data");
                }
                else
                {
                    e.Acknowledge(asmDataConfig.Name);
                }
            }
        }

        private void AsmDataProductConfigChanged(object sender, ProductConfigChangedEventArgs e)
        {
            var asmDataConfigs = e.GetDeserializedConfigurations<RcmModels.AsmData.Payload>();
            foreach (var asmDataConfig in asmDataConfigs)
            {
                if (asmDataConfig.TypedFile?.RulesData?.Length > 0)
                {
                    _remoteConfigurationStatus.RulesDataByFile[asmDataConfig.Name] = asmDataConfig.TypedFile.RulesData;
                }
            }

            var updated = true;
            if (_enabled)
            {
                var ruleData = _remoteConfigurationStatus.RulesDataByFile.SelectMany(x => x.Value).ToList();
                updated = UpdateWafWithRulesData(ruleData);
            }

            foreach (var asmDataConfig in asmDataConfigs)
            {
                if (!updated)
                {
                    e.Error(asmDataConfig.Name, "Waf could not update the rules data");
                }
                else
                {
                    e.Acknowledge(asmDataConfig.Name);
                }
            }
        }

        private void AsmProductConfigRemoved(object sender, ProductConfigChangedEventArgs e)
        {
            var asmConfigs = e.GetDeserializedConfigurations<RcmModels.Asm.Payload>();
            Dictionary<string, List<string>> customAttributesToRemove = new();

            foreach (var asmConfig in asmConfigs)
            {
                if (_remoteConfigurationStatus.RulesOverridesByFile.ContainsKey(asmConfig.Name))
                {
                    _remoteConfigurationStatus.RulesOverridesByFile.Remove(asmConfig.Name);
                }

                if (_remoteConfigurationStatus.ExclusionsByFile.ContainsKey(asmConfig.Name))
                {
                    _remoteConfigurationStatus.ExclusionsByFile.Remove(asmConfig.Name);
                }

                if (_remoteConfigurationStatus.CustomAttributes.ContainsKey(asmConfig.Name))
                {
                    customAttributesToRemove[asmConfig.Name] = _remoteConfigurationStatus.CustomAttributes[asmConfig.Name].Keys.ToList();
                    _remoteConfigurationStatus.CustomAttributes.Remove(asmConfig.Name);
                }
            }

            var result = true;
            if (_enabled)
            {
                var overrides = _remoteConfigurationStatus.RulesOverridesByFile.SelectMany(x => x.Value).ToList();
                var exclusions = _remoteConfigurationStatus.ExclusionsByFile.SelectMany(x => x.Value).ToList();

                result = _waf.UpdateRulesStatus(overrides, exclusions);
                Log.Debug<bool, int, int>(
                    "_waf.Update was updated for removal: {Success}, ({RulesOverridesCount} rule status entries), ({ExclusionsCount} exclusion filters)",
                    result,
                    overrides.Count,
                    exclusions.Count);

                HandleRemoveRcmAttributes(customAttributesToRemove);
            }

            foreach (var asmConfig in asmConfigs)
            {
                if (result)
                {
                    e.Acknowledge(asmConfig.Name);
                }
                else
                {
                    e.Error(asmConfig.Name, "waf couldn't be remove with rule asm product");
                }
            }
        }

        private void AsmProductConfigChanged(object sender, ProductConfigChangedEventArgs e)
        {
            var asmConfigs = e.GetDeserializedConfigurations<RcmModels.Asm.Payload>();

            foreach (var asmConfig in asmConfigs)
            {
                if (asmConfig.TypedFile.RuleOverrides?.Length > 0)
                {
                    _remoteConfigurationStatus.RulesOverridesByFile[asmConfig.Name] = asmConfig.TypedFile.RuleOverrides;
                }

                if (asmConfig.TypedFile.Exclusions?.Count > 0)
                {
                    _remoteConfigurationStatus.ExclusionsByFile[asmConfig.Name] = asmConfig.TypedFile.Exclusions;
                }

                if (asmConfig.TypedFile.Actions != null)
                {
                    foreach (var action in asmConfig.TypedFile.Actions)
                    {
                        if (action.Id is not null)
                        {
                            _remoteConfigurationStatus.Actions[action.Id] = action;
                        }
                    }

                    if (asmConfig.TypedFile.Actions.Length == 0)
                    {
                        _remoteConfigurationStatus.Actions.Clear();
                    }
                }

                if (asmConfig.TypedFile.CustomAttributes != null && asmConfig.TypedFile.CustomAttributes.Attributes != null)
                {
                    Dictionary<string, object> attributes = new();
                    foreach (var key in asmConfig.TypedFile.CustomAttributes.Attributes.Keys)
                    {
                        attributes.Add(key, asmConfig.TypedFile.CustomAttributes.Attributes[key]);
                    }

                    _remoteConfigurationStatus.CustomAttributes[asmConfig.Name] = attributes;
                }
            }

            var result = true;
            if (_enabled)
            {
                var overrides = _remoteConfigurationStatus.RulesOverridesByFile.SelectMany(x => x.Value).ToList();
                var exclusions = _remoteConfigurationStatus.ExclusionsByFile.SelectMany(x => x.Value).ToList();

                result = _waf.UpdateRulesStatus(overrides, exclusions);
                Log.Debug<bool, int, int>(
                    "_waf.Update was updated for change: {Success}, ({RulesOverridesCount} rule status entries), ({ExclusionsCount} exclusion filters)",
                    result,
                    overrides.Count,
                    exclusions.Count);

                // Handle custom attributes
                HandleSetRcmAttributes();
            }

            foreach (var asmConfig in asmConfigs)
            {
                if (result)
                {
                    e.Acknowledge(asmConfig.Name);
                }
                else
                {
                    e.Error(asmConfig.Name, "waf couldn't be updated with asm product");
                }
            }
        }

        private void HandleSetRcmAttributes()
        {
            // Define attributes constants
            const string wafTimeoutKey = "waf_timeout";

            // Set attributes
            foreach (var configName in _remoteConfigurationStatus.CustomAttributes.Keys)
            {
                foreach (var attribute in _remoteConfigurationStatus.CustomAttributes[configName].Keys)
                {
                    switch (attribute)
                    {
                        case wafTimeoutKey:
                            var oldValue = SetWafTimeout(_remoteConfigurationStatus.CustomAttributes[configName][attribute]);

                            if (!_remoteConfigurationStatus.OldAttributes.ContainsKey(configName))
                            {
                                _remoteConfigurationStatus.OldAttributes[configName] = new();
                            }

                            // Only set the old value if it's hasn't been registered before
                            if (!_remoteConfigurationStatus.OldAttributes[configName].ContainsKey(attribute))
                            {
                                _remoteConfigurationStatus.OldAttributes[configName][attribute] = oldValue;
                            }

                            break;
                    }
                }
            }

            object SetWafTimeout(object value)
            {
                object oldValue = null;
                try
                {
                    var wafTimeoutValue = Convert.ToUInt64(value);
                    if (wafTimeoutValue <= 0)
                    {
                        Log.Warning("Ignoring '{WafTimeoutKey}' of '{WafTimeoutString}' because it was zero or less", wafTimeoutKey, wafTimeoutValue);
                    }
                    else
                    {
                        oldValue = _settings.WafTimeoutMicroSeconds;
                        _settings.WafTimeoutMicroSeconds = wafTimeoutValue;
                        Log.Debug("The {WafTimeoutMicroSecondsKey} has been set to '{NewWafTimeoutValue}' according to the attribute '{WafTimeoutKey}'", nameof(_settings.WafTimeoutMicroSeconds), wafTimeoutValue, wafTimeoutKey);
                    }
                }
                catch (Exception e)
                {
                    Log.Warning("The WafTimeoutMicroSecondsKey failed to be set according to the attribute '{WafTimeoutKey}': {Error}: {TimeoutValue}", wafTimeoutKey, e.Message, value);
                }

                return oldValue;
            }
        }

        private void HandleRemoveRcmAttributes(IReadOnlyDictionary<string, List<string>> attributesConfig)
        {
            if (attributesConfig is null)
            {
                return;
            }

            foreach (var configName in attributesConfig.Keys)
            {
                foreach (var attribute in attributesConfig[configName])
                {
                    switch (attribute)
                    {
                        case "waf_timeout":
                            RemoveWafTimeout(configName, "waf_timeout");
                            break;
                    }
                }
            }

            void RemoveWafTimeout(string configName, string attribute)
            {
                // Reset the waf timeout to default value
                var oldValue = (ulong)_remoteConfigurationStatus.OldAttributes[configName][attribute];
                _settings.WafTimeoutMicroSeconds = oldValue;

                Log.Debug("The WafTimeoutMicroSecondsKey has been reset to its previous value: {OldValue}", oldValue);
            }
        }

        private bool UpdateWafWithRulesData(List<RuleData> ruleData) => _waf?.UpdateRulesData(ruleData) ?? false;

        private void InitWafAndInstrumentations(bool fromRemoteConfig = false)
        {
            // initialization of WafLibraryInvoker
            if (_libraryInitializationResult == null)
            {
                _libraryInitializationResult = WafLibraryInvoker.Initialize();
                if (!_libraryInitializationResult.Success)
                {
                    _settings.Enabled = false;
                    // logs happened during the process of initializing
                    return;
                }

                _wafLibraryInvoker = _libraryInitializationResult.WafLibraryInvoker;
            }

            _wafInitResult = Waf.Waf.Create(_wafLibraryInvoker!, _settings.ObfuscationParameterKeyRegex, _settings.ObfuscationParameterValueRegex, _settings.Rules, _remoteConfigurationStatus.RemoteRulesJson);
            if (_wafInitResult.Success)
            {
                WafRuleFileVersion = _wafInitResult.RuleFileVersion;
                var oldWaf = _waf;
                _waf = _wafInitResult.Waf;
                oldWaf?.Dispose();
                Log.Debug("Disposed old waf and affected new waf");
                var ruleData = _remoteConfigurationStatus.RulesDataByFile.SelectMany(x => x.Value).ToList();
                UpdateWafWithRulesData(ruleData);
                AddInstrumentationsAndProducts(fromRemoteConfig);
            }
            else
            {
                _wafInitResult.Waf?.Dispose();
                _settings.Enabled = false;
            }
        }

        private void DisposeWafAndInstrumentations(bool fromRemoteConfig = false)
        {
            RemoveInstrumentationsAndProducts(fromRemoteConfig);
            _waf?.Dispose();
            _waf = null;
        }

        private void AddInstrumentationsAndProducts(bool fromRemoteConfig)
        {
            if (!_enabled)
            {
                AsmRemoteConfigurationProducts.AsmDataProduct.ConfigChanged += AsmDataProductConfigChanged;
                AsmRemoteConfigurationProducts.AsmProduct.ConfigChanged += AsmProductConfigChanged;
                AsmRemoteConfigurationProducts.AsmDataProduct.ConfigRemoved += AsmDataProductConfigRemoved;
                AsmRemoteConfigurationProducts.AsmProduct.ConfigRemoved += AsmProductConfigRemoved;
                AddAppsecSpecificInstrumentations();

                _rateLimiter ??= new AppSecRateLimiter(_settings.TraceRateLimit);

                _enabled = true;

                Log.Information("AppSec is now Enabled, _settings.Enabled is {EnabledValue}, coming from remote config: {EnableFromRemoteConfig}", _settings.Enabled, fromRemoteConfig);
            }
        }

        private void RemoveInstrumentationsAndProducts(bool fromRemoteConfig)
        {
            if (_enabled)
            {
                AsmRemoteConfigurationProducts.AsmDataProduct.ConfigChanged -= AsmDataProductConfigChanged;
                AsmRemoteConfigurationProducts.AsmProduct.ConfigChanged -= AsmProductConfigChanged;
                AsmRemoteConfigurationProducts.AsmDataProduct.ConfigRemoved -= AsmDataProductConfigRemoved;
                AsmRemoteConfigurationProducts.AsmProduct.ConfigRemoved -= AsmProductConfigRemoved;
                RemoveAppsecSpecificInstrumentations();

                _enabled = false;

                Log.Information("AppSec is now Disabled, _settings.Enabled is {EnabledValue}, coming from remote config: {EnableFromRemoteConfig}", _settings.Enabled, fromRemoteConfig);
            }
        }

        internal void SetTraceSamplingPriority(Span span)
        {
            if (!_settings.KeepTraces)
            {
                // NOTE: setting DD_APPSEC_KEEP_TRACES=false means "drop all traces by setting AutoReject".
                // It does _not_ mean "stop setting UserKeep (do nothing)". It should only be used for testing.
                span.Context.TraceContext?.SetSamplingPriority(SamplingPriorityValues.AutoReject, SamplingMechanism.Asm);
            }
            else if (_rateLimiter.Allowed(span))
            {
                span.Context.TraceContext?.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Asm);
            }
        }

        internal IContext CreateAdditiveContext() => _waf?.CreateContext();

        private void RunShutdown()
        {
            AsmRemoteConfigurationProducts.AsmDataProduct.ConfigChanged -= AsmDataProductConfigChanged;
            AsmRemoteConfigurationProducts.AsmProduct.ConfigChanged -= AsmProductConfigChanged;
            AsmRemoteConfigurationProducts.AsmDataProduct.ConfigRemoved -= AsmDataProductConfigRemoved;
            AsmRemoteConfigurationProducts.AsmProduct.ConfigRemoved -= AsmProductConfigRemoved;
            AsmRemoteConfigurationProducts.AsmFeaturesProduct.ConfigChanged -= FeaturesProductConfigChanged;
            AsmRemoteConfigurationProducts.AsmDDProduct.ConfigChanged -= AsmDDProductConfigChanged;
            Dispose();
        }
    }
}
