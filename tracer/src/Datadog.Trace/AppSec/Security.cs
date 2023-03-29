// <copyright file="Security.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.AsmDd;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Sampling;
using Action = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;

namespace Datadog.Trace.AppSec
{
    /// <summary>
    /// The Secure is responsible coordinating ASM
    /// </summary>
    internal class Security : IDatadogSecurity, IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Security>();

        private static Security? _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();
        private readonly SecuritySettings _settings;
        private readonly Dictionary<string, AsmRemoteConfigurationProduct> _products = AsmRemoteConfigurationProducts.GetAll();
        private readonly ConfigurationStatus _configurationStatus;
        private readonly RemoteConfigurationManager? _remoteConfigurationManager;
        private Subscription? _rcmSubscription;
        private LibraryInitializationResult? _libraryInitializationResult;
        private IWaf? _waf;
        private WafLibraryInvoker? _wafLibraryInvoker;
        private AppSecRateLimiter? _rateLimiter;
        private InitResult? _wafInitResult;

        /// <summary>
        /// Initializes a new instance of the <see cref="Security"/> class with default settings.
        /// </summary>
        public Security(SecuritySettings? settings = null, IWaf? waf = null, IDictionary<string, Action>? actions = null)
            : this(settings, waf)
        {
            if (actions != null)
            {
                _configurationStatus.Actions = actions;
            }
        }

        private Security(SecuritySettings? settings = null, IWaf? waf = null)
        {
            _remoteConfigurationManager = RemoteConfigurationManager.Instance;
            try
            {
                _settings = settings ?? SecuritySettings.FromDefaultSources();
                _configurationStatus = new ConfigurationStatus(_settings.Rules);
                Log.Information("anna: initialize security");
                _waf = waf;
                LifetimeManager.Instance.AddShutdownTask(RunShutdown);
                Log.Information("anna: here {CanBeEnabled}", _settings.CanBeToggled);

                if (_settings.Enabled1 && _waf == null)
                {
                    InitWafAndInstrumentations();
                }
                else
                {
                    Log.Information("AppSec remote enabling not allowed (DD_APPSEC_ENABLED=false).");
                }

                if (_settings.CanBeToggled)
                {
                    SubscribeToChanges(AsmRemoteConfigurationProducts.AsmFeaturesProduct.Name, AsmRemoteConfigurationProducts.AsmDdProduct.Name);
                }

                SetRemoteConfigCapabilites();
            }
            catch (Exception ex)
            {
                _settings ??= new(source: null);
                _configurationStatus ??= new ConfigurationStatus(string.Empty);
                Log.Error(ex, "DDAS-0001-01: AppSec could not start because of an unexpected error. No security activities will be collected. Please contact support at https://docs.datadoghq.com/help/ for help.");
            }
        }

        /// <summary>
        /// Gets or sets the global <see cref="Security"/> instance.
        /// </summary>
        public static Security Instance
        {
            get => LazyInitializer.EnsureInitialized(ref _instance!, ref _globalInstanceInitialized, ref _globalInstanceLock, () => new Security(null, null));

            set
            {
                lock (_globalInstanceLock)
                {
                    _instance = value;
                    _globalInstanceInitialized = true;
                }
            }
        }

        internal bool Enabled { get; private set; }

        internal bool WafExportsErrorHappened => _libraryInitializationResult?.ExportErrorHappened ?? false;

        internal string? WafRuleFileVersion { get; private set; }

        internal InitResult? WafInitResult => _wafInitResult;

        /// <summary>
        /// Gets <see cref="SecuritySettings"/> instance
        /// </summary>
        SecuritySettings IDatadogSecurity.Settings => _settings;

        internal SecuritySettings Settings => _settings;

        internal string? DdlibWafVersion => _waf?.Version;

        internal void SubscribeToChanges(params string[] productNames)
        {
            void RcmSubscription(RemoteConfigurationManager remoteConfigurationManager)
            {
                if (_rcmSubscription is null)
                {
                    _rcmSubscription = remoteConfigurationManager.SubscribeToChanges(
                        UpdateFromRcm,
                        productNames);
                }
                else
                {
                    _rcmSubscription.SubscribeProducts(productNames);
                }
            }

            if (_remoteConfigurationManager is null)
            {
                RemoteConfigurationManager.CallbackWithInitializedInstance(RcmSubscription);
            }
            else
            {
                RcmSubscription(_remoteConfigurationManager);
            }
        }

        internal List<ApplyDetails> UpdateFromRcm(Dictionary<string, List<RemoteConfiguration>> configsByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigs)
        {
            foreach (var product in _products)
            {
                configsByProduct.TryGetValue(product.Key, out var config);

                var removedConfigsForThisProduct = removedConfigs?[product.Key];
                product.Value.UpdateRemoteConfigurationStatus(config, removedConfigsForThisProduct, _configurationStatus);
            }

            if (_configurationStatus.IncomingUpdateState.SecurityStateChange)
            {
                if (Enabled && _configurationStatus.EnableAsm == false)
                {
                    DisposeWafAndInstrumentations(true);
                }
                else if (!Enabled && _configurationStatus.EnableAsm == true)
                {
                    InitWafAndInstrumentations(true);
                    // no point in updating the waf with potentially new rules as it's initialized here with new rules
                    _configurationStatus.IncomingUpdateState.WafKeysToUpdate.Remove(ConfigurationStatus.WafRulesKey);
                }
            }

            var acknowledge = false;
            UpdateResult? result = null;
            if (Enabled && _configurationStatus.IncomingUpdateState.WafKeysToUpdate.Any())
            {
                result = _waf?.UpdateWafFromConfigurationStatus(_configurationStatus);
                if (result?.Success ?? false)
                {
                    _configurationStatus.ResetUpdateMarkers();
                    acknowledge = true;
                }
            }
            else
            {
                // acknowledge because it's been applied in memory and will be reapplied when enabled since we dont reset configurationStatus
                acknowledge = true;
            }

            var applyDetails = new List<ApplyDetails>();
            var allRemoteConfigurations = configsByProduct.SelectMany(c => c.Value);
            if (acknowledge)
            {
                foreach (var config in allRemoteConfigurations)
                {
                    applyDetails.Add(ApplyDetails.FromOk(config.Path.Path));
                }
            }
            else
            {
                foreach (var config in allRemoteConfigurations)
                {
                    applyDetails.Add(ApplyDetails.FromError(config.Path.Path, result?.ErrorMessage));
                }
            }

            return applyDetails;
        }

        internal BlockingAction GetBlockingAction(string id, string[]? requestAcceptHeaders)
        {
            var blockingAction = new BlockingAction();
            _configurationStatus.Actions.TryGetValue(id, out var action);

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
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmActivation, _settings.CanBeToggled);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmDdRules, noLocalRules);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmIpBlocking, noLocalRules);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmExclusion, noLocalRules);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmRequestBlocking, noLocalRules);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmResponseBlocking, noLocalRules);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmCustomBlockingResponse, noLocalRules);
                });
        }

        private void InitWafAndInstrumentations(bool fromRemoteConfig = false)
        {
            // initialization of WafLibraryInvoker
            if (_libraryInitializationResult == null)
            {
                _libraryInitializationResult = WafLibraryInvoker.Initialize();
                if (!_libraryInitializationResult.Success)
                {
                    Enabled = false;
                    // logs happened during the process of initializing
                    return;
                }

                _wafLibraryInvoker = _libraryInitializationResult.WafLibraryInvoker;
            }

            _wafInitResult = Waf.Waf.Create(_wafLibraryInvoker!, _settings.ObfuscationParameterKeyRegex, _settings.ObfuscationParameterValueRegex, _settings.Rules, _configurationStatus.RulesByFile.Values.FirstOrDefault()?.All);
            if (_wafInitResult.Success)
            {
                // we don't reapply configurations to the waf here because it's all done in the subscription function, as new data might have been received at the same time as the enable command, we don't want to update twice (here and in the subscription)
                WafRuleFileVersion = _wafInitResult.RuleFileVersion;
                var oldWaf = _waf;
                _waf = _wafInitResult.Waf;
                oldWaf?.Dispose();
                Log.Debug("Disposed old waf and affected new waf");
                SubscribeToChanges(AsmRemoteConfigurationProducts.AsmDataProduct.Name, AsmRemoteConfigurationProducts.AsmProduct.Name);
                AddAppsecSpecificInstrumentations();
                _rateLimiter ??= new(_settings.TraceRateLimit);
                Enabled = true;
                Log.Information("AppSec is now Enabled, _settings.Enabled is {EnabledValue}, coming from remote config: {EnableFromRemoteConfig}", _settings.Enabled1, fromRemoteConfig);
                if (_wafInitResult.EmbeddedRules != null)
                {
                    _configurationStatus.FallbackEmbeddedRuleSet ??= RuleSet.From(_wafInitResult.EmbeddedRules);
                }
            }
            else
            {
                _wafInitResult.Waf?.Dispose();
                Enabled = false;
            }
        }

        private void DisposeWafAndInstrumentations(bool fromRemoteConfig = false)
        {
            RemoveInstrumentationsAndProducts(fromRemoteConfig);
            _waf?.Dispose();
            _waf = null;
        }

        private void RemoveInstrumentationsAndProducts(bool fromRemoteConfig)
        {
            if (Enabled)
            {
                _rcmSubscription?.UnsubscribeProducts(AsmRemoteConfigurationProducts.AsmDataProduct.Name, AsmRemoteConfigurationProducts.AsmProduct.Name);

                RemoveAppsecSpecificInstrumentations();

                Enabled = false;

                Log.Information("AppSec is now Disabled, _settings.Enabled is {EnabledValue}, coming from remote config: {EnableFromRemoteConfig}", _settings.Enabled1, fromRemoteConfig);
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
            else if (_rateLimiter?.Allowed(span) ?? false)
            {
                span.Context.TraceContext?.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Asm);
            }
        }

        internal IContext? CreateAdditiveContext() => _waf?.CreateContext();

        private void RunShutdown()
        {
            if (_rcmSubscription != null)
            {
                _remoteConfigurationManager?.UnsubscribeToChanges(_rcmSubscription);
            }

            Dispose();
        }
    }
}
