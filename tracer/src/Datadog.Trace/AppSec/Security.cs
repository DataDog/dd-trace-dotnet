// <copyright file="Security.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypesManaged;
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
        private readonly Concurrency.ReaderWriterLock _wafLocker = new();
        private readonly SecuritySettings _settings;
        private LibraryInitializationResult _libraryInitializationResult;
        private IWaf _waf;
        private WafLibraryInvoker _wafLibraryInvoker;
        private AppSecRateLimiter _rateLimiter;
        private bool _enabled = false;
        private IDictionary<string, Payload> _asmDataConfigs = new Dictionary<string, RcmModels.AsmData.Payload>();
        private IDictionary<string, bool> _ruleStatus = null;
        private string _remoteRulesJson = null;
        private InitializationResult _wafInitializationResult;

        static Security()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Security"/> class with default settings.
        /// </summary>
        public Security()
            : this(null, null)
        {
        }

        private Security(SecuritySettings settings = null, IWaf waf = null)
        {
            try
            {
                _settings = settings ?? SecuritySettings.FromDefaultSources();
                _waf = waf;
                LifetimeManager.Instance.AddShutdownTask(RunShutdown);

                if (_settings.CanBeEnabled)
                {
                    UpdateStatus();
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
            get => LazyInitializer.EnsureInitialized(ref _instance, ref _globalInstanceInitialized, ref _globalInstanceLock);

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

        internal string WafRuleFileVersion => _wafInitializationResult?.RuleFileVersion;

        internal InitializationResult WafInitResult => _wafInitializationResult;

        /// <summary>
        /// Gets <see cref="SecuritySettings"/> instance
        /// </summary>
        SecuritySettings IDatadogSecurity.Settings => _settings;

        internal SecuritySettings Settings => _settings;

        internal string DdlibWafVersion => _waf?.Version;

        internal IWaf CurrentWaf => _waf;

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
                Log.Error(ex, ex.Message);
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
                Log.Error(ex, ex.Message);
            }

            Log.Information($"{defs} AppSec definitions and {derived} AppSec derived definitions added to the profiler.");
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
                Log.Error(ex, ex.Message);
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
                Log.Error(ex, ex.Message);
            }

            Log.Information($"{defs} AppSec definitions and {derived} AppSec derived definitions removed from the profiler.");
        }

        /// <summary> Frees resources </summary>
        public void Dispose() => _waf?.Dispose();

        private void SetRemoteConfigCapabilites()
        {
            RemoteConfigurationManager.CallbackWithInitializedInstance(
                rcm =>
                {
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmActivation, _settings.CanBeEnabled);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmDdRules, _settings.Rules == null);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmIpBlocking, true);
                });
        }

        private void AsmDDProductConfigChanged(object sender, ProductConfigChangedEventArgs e)
        {
            var asmDD = e.GetConfigurationAsString().FirstOrDefault();
            if (!string.IsNullOrEmpty(asmDD.TypedFile))
            {
                _remoteRulesJson = asmDD.TypedFile;
                UpdateStatus(true);
            }

            e.Acknowledge(asmDD.Name);
        }

        private void FeaturesProductConfigChanged(object sender, ProductConfigChangedEventArgs e)
        {
            var features = e.GetDeserializedConfigurations<AsmFeatures>().FirstOrDefault();
            if (features.TypedFile != null)
            {
                _settings.Enabled = features.TypedFile.Asm.Enabled;
                UpdateStatus(true);
            }

            e.Acknowledge(features.Name);
        }

        private void AsmDataProductConfigChanged(object sender, ProductConfigChangedEventArgs e)
        {
            if (!_enabled)
            {
                return;
            }

            _asmDataConfigs ??= new Dictionary<string, Payload>();
            var asmDataConfigs = e.GetDeserializedConfigurations<Payload>();
            foreach (var asmDataConfig in asmDataConfigs)
            {
                _asmDataConfigs[asmDataConfig.Name] = asmDataConfig.TypedFile;
                e.Acknowledge(asmDataConfig.Name);
            }

            var updated = UpdateRulesData();
            foreach (var asmDataConfig in asmDataConfigs)
            {
                if (!updated)
                {
                    e.Error(asmDataConfig.Name, "Waf could not update the rules");
                }
                else
                {
                    e.Acknowledge(asmDataConfig.Name);
                }
            }
        }

        private void AsmProductConfigChanged(object sender, ProductConfigChangedEventArgs e)
        {
            if (!_enabled) { return; }

            var asmConfigs = e.GetDeserializedConfigurations<RcmModels.Asm.Payload>();
            int ruleCount = 0;
            var ruleStatus = new Dictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var asmConfig in asmConfigs)
            {
                try
                {
                    var rulesStatus = asmConfig.TypedFile.RuleStatus;
                    foreach (var data in rulesStatus)
                    {
                        if (data.Id == null || data.Enabled == null)
                        {
                            var id = data.Id ?? "NULL";
                            var enabled = data.Enabled?.ToString() ?? "NULL";
                            e.Error(asmConfig.Name, $"Received Null values on message ({id}={enabled}).");
                            continue;
                        }

                        ruleStatus[data.Id] = data.Enabled.Value;
                        ruleCount++;
                    }

                    if (ruleCount > 0)
                    {
                        e.Acknowledge(asmConfig.Name);
                    }
                    else
                    {
                        e.Error(asmConfig.Name, "No valid Waf rule status data received.");
                    }
                }
                catch (Exception err)
                {
                    e.Error(asmConfig.Name, "Waf rule status data error: " + err.Message);
                }
            }

            _ruleStatus = new ReadOnlyDictionary<string, bool>(ruleStatus);
            UpdateRuleStatus(_ruleStatus);
        }

        private bool UpdateRulesData()
        {
            bool res = false;
            lock (_asmDataConfigs)
            {
                res = _waf?.UpdateRulesData(_asmDataConfigs?.SelectMany(p => p.Value.RulesData)) ?? false;
            }

            UpdateRuleStatus(_ruleStatus);
            return res;
        }

        private void UpdateRuleStatus(IDictionary<string, bool> ruleStatus)
        {
            if (ruleStatus is { Count: > 0 } && !(_waf?.ToggleRules(ruleStatus) ?? false))
            {
                Log.Debug($"_waf.ToggleRules returned false ({ruleStatus.Count} rule status entries)");
            }
        }

        private void UpdateStatus(bool fromRemoteConfig = false)
        {
            if (_settings.Enabled)
            {
                if (_libraryInitializationResult == null)
                {
                    _libraryInitializationResult = WafLibraryInvoker.Initialize();
                    if (!_libraryInitializationResult.Success)
                    {
                        _settings.Enabled = false;
                        _wafLibraryInvoker = _libraryInitializationResult.WafLibraryInvoker;
                        // logs happened during the process of initializing
                        return;
                    }
                }

                _wafInitializationResult = Waf.Waf.Create(_wafLibraryInvoker, _settings.ObfuscationParameterKeyRegex, _settings.ObfuscationParameterValueRegex, _settings.Rules, _remoteRulesJson);
                if (_wafInitializationResult.Success && _wafLocker.EnterWriteLock())
                {
                    var oldWaf = _waf;
                    _waf = _wafInitializationResult.Waf;
                    oldWaf?.Dispose();
                    _wafLocker.ExitWriteLock();

                    Log.Debug("Disposed old waf and affected new waf");

                    UpdateRulesData();
                    AddInstrumentationsAndProducts(fromRemoteConfig);
                }
                else
                {
                    _wafLocker.EnterWriteLock();
                    _waf?.Dispose();
                    _wafLocker.ExitWriteLock();
                    _settings.Enabled = false;
                }
            }

            if (!_settings.Enabled)
            {
                RemoveInstrumentationsAndProducts(fromRemoteConfig);
            }
        }

        private void AddInstrumentationsAndProducts(bool fromRemoteConfig)
        {
            if (!_enabled)
            {
                AsmRemoteConfigurationProducts.AsmDataProduct.ConfigChanged += AsmDataProductConfigChanged;
                AsmRemoteConfigurationProducts.AsmProduct.ConfigChanged += AsmProductConfigChanged;
                AddAppsecSpecificInstrumentations();

                _rateLimiter ??= new AppSecRateLimiter(_settings.TraceRateLimit);

                _enabled = true;

                Log.Information("AppSec is now Enabled, _settings.Enabled is {EnabledValue}, coming from remote config: {enableFromRemoteConfig}", _settings.Enabled, fromRemoteConfig);
            }
        }

        private void RemoveInstrumentationsAndProducts(bool fromRemoteConfig)
        {
            if (_enabled)
            {
                AsmRemoteConfigurationProducts.AsmDataProduct.ConfigChanged -= AsmDataProductConfigChanged;
                AsmRemoteConfigurationProducts.AsmProduct.ConfigChanged -= AsmProductConfigChanged;
                RemoveAppsecSpecificInstrumentations();

                _enabled = false;

                Log.Information("AppSec is now Disabled, _settings.Enabled is {EnabledValue}, coming from remote config: {enableFromRemoteConfig}", _settings.Enabled, fromRemoteConfig);
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

        internal IContext CreateAdditiveContext() => _waf?.CreateContext(_wafLocker);

        private void RunShutdown()
        {
            AsmRemoteConfigurationProducts.AsmDataProduct.ConfigChanged -= AsmDataProductConfigChanged;
            AsmRemoteConfigurationProducts.AsmProduct.ConfigChanged -= AsmProductConfigChanged;
            AsmRemoteConfigurationProducts.AsmFeaturesProduct.ConfigChanged -= FeaturesProductConfigChanged;
            AsmRemoteConfigurationProducts.AsmDDProduct.ConfigChanged -= AsmDDProductConfigChanged;
            _wafLocker.Dispose();
            Dispose();
        }
    }
}
