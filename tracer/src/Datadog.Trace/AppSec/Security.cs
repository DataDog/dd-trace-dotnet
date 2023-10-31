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
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
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
        private readonly IReadOnlyDictionary<string, IAsmConfigUpdater> _productConfigUpdaters = new Dictionary<string, IAsmConfigUpdater> { { RcmProducts.AsmFeatures, new AsmFeaturesProduct() }, { RcmProducts.Asm, new AsmProduct() }, { RcmProducts.AsmDd, new AsmDdProduct() }, { RcmProducts.AsmData, new AsmDataProduct() } };

        private readonly ConfigurationStatus _configurationStatus;
        private readonly bool _noLocalRules;
        private readonly IRcmSubscriptionManager _rcmSubscriptionManager;
        private ISubscription? _rcmSubscription;
        private LibraryInitializationResult? _libraryInitializationResult;
        private IWaf? _waf;
        private WafLibraryInvoker? _wafLibraryInvoker;
        private AppSecRateLimiter? _rateLimiter;
        private InitResult? _wafInitResult;

        /// <summary>
        /// Initializes a new instance of the <see cref="Security"/> class with default settings.
        /// </summary>
        public Security(SecuritySettings? settings = null, IWaf? waf = null, IDictionary<string, Action>? actions = null, IRcmSubscriptionManager? rcmSubscriptionManager = null)
            : this(settings, waf, rcmSubscriptionManager)
        {
            if (actions != null)
            {
                _configurationStatus.Actions = actions;
            }
        }

        private Security(SecuritySettings? settings = null, IWaf? waf = null, IRcmSubscriptionManager? rcmSubscriptionManager = null)
        {
            _rcmSubscriptionManager = rcmSubscriptionManager ?? RcmSubscriptionManager.Instance;
            try
            {
                _settings = settings ?? SecuritySettings.FromDefaultSources();
                _waf = waf;
                _noLocalRules = _settings.Rules == null;
                _configurationStatus = new ConfigurationStatus(_settings.Rules);
                LifetimeManager.Instance.AddShutdownTask(RunShutdown);

                if (_settings.Enabled && _waf == null)
                {
                    InitWafAndInstrumentations();
                }
                else
                {
                    Log.Information("AppSec was not activated, its status is enabled={AppSecEnabled}, AppSec can be remotely enabled={CanBeRcEnabled}.", Enabled, _settings.CanBeToggled);
                }

                var subscriptionsKeys = new List<string>();
                if (_settings.CanBeToggled)
                {
                    subscriptionsKeys.Add(RcmProducts.AsmFeatures);
                }

                if ((_settings.Enabled || _settings.CanBeToggled) && _noLocalRules)
                {
                    subscriptionsKeys.Add(RcmProducts.AsmDd);
                }

                SubscribeToChanges(subscriptionsKeys.ToArray());

                SetRemoteConfigCapabilites();
            }
            catch (Exception ex)
            {
                _settings ??= new(source: null, TelemetryFactory.Config);
                _configurationStatus ??= new ConfigurationStatus(string.Empty);
                Log.Error(ex, "DDAS-0001-01: AppSec could not start because of an unexpected error. No security activities will be collected. Please contact support at https://docs.datadoghq.com/help/ for help.");
            }
            finally
            {
                _settings ??= new(source: null, TelemetryFactory.Config);
                ApiSecurity = new(_settings);
            }
        }

        /// <summary>
        /// Gets or sets the global <see cref="Security"/> instance.
        /// </summary>
        public static Security Instance
        {
            get => LazyInitializer.EnsureInitialized(ref _instance!, ref _globalInstanceInitialized, ref _globalInstanceLock, () => new Security(null, null, null));

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

        internal string? InitializationError { get; private set; }

        internal bool WafExportsErrorHappened => _libraryInitializationResult?.ExportErrorHappened ?? false;

        internal string? WafRuleFileVersion { get; private set; }

        internal InitResult? WafInitResult => _wafInitResult;

        /// <summary>
        /// Gets <see cref="SecuritySettings"/> instance
        /// </summary>
        SecuritySettings IDatadogSecurity.Settings => _settings;

        internal SecuritySettings Settings => _settings;

        internal string? DdlibWafVersion => _waf?.Version;

        internal bool TrackUserEvents => Enabled && Settings.UserEventsAutomatedTracking != "disabled";

        internal bool IsExtendedUserTrackingEnabled => Settings.UserEventsAutomatedTracking == SecuritySettings.UserTrackingExtendedMode;

        internal ApiSecurity ApiSecurity { get; }

        internal void SubscribeToChanges(params string[] productNames)
        {
            if (_rcmSubscription is not null)
            {
                var newSubscription = new Subscription(UpdateFromRcm, _rcmSubscription.ProductKeys.Union(productNames).ToArray());
                _rcmSubscriptionManager.Replace(_rcmSubscription, newSubscription);
                _rcmSubscription = newSubscription;
            }
            else
            {
                _rcmSubscription = new Subscription(UpdateFromRcm, productNames.ToArray());
                _rcmSubscriptionManager.SubscribeToChanges(_rcmSubscription);
            }
        }

        internal IEnumerable<ApplyDetails> UpdateFromRcm(Dictionary<string, List<RemoteConfiguration>> configsByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigs)
        {
            string? rcmUpdateError = null;
            UpdateResult? updateResult = null;

            try
            {
                foreach (var product in _productConfigUpdaters)
                {
                    if (configsByProduct.TryGetValue(product.Key, out var configurations))
                    {
                        product.Value.ProcessUpdates(_configurationStatus, configurations);
                    }

                    if (removedConfigs?.TryGetValue(product.Key, out var configsForThisProductToRemove) is true)
                    {
                        product.Value.ProcessRemovals(_configurationStatus, configsForThisProductToRemove);
                    }
                }

                _configurationStatus.EnableAsm = !_configurationStatus.AsmFeaturesByFile.IsEmpty() && _configurationStatus.AsmFeaturesByFile.All(a => a.Value.Enabled == true);

                // normally CanBeToggled should not need a check as asm_features capacity is only sent if AppSec env var is null, but still guards it in case
                if (_configurationStatus.IncomingUpdateState.SecurityStateChange && _settings.CanBeToggled)
                {
                    if (Enabled && _configurationStatus.EnableAsm == false)
                    {
                        DisposeWafAndInstrumentations(true);
                        _configurationStatus.IncomingUpdateState.SecurityStateChange = false;
                    }
                    else if (!Enabled && _configurationStatus.EnableAsm == true)
                    {
                        InitWafAndInstrumentations(true);
                        rcmUpdateError = _wafInitResult?.ErrorMessage;
                        // no point in updating the waf with potentially new rules as it's initialized here with new rules
                        _configurationStatus.IncomingUpdateState.WafKeysToApply.Remove(ConfigurationStatus.WafRulesKey);
                        // reapply others
                        _configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafExclusionsKey);
                        _configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesDataKey);
                        _configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesOverridesKey);
                        _configurationStatus.IncomingUpdateState.SecurityStateChange = false;
                    }
                }

                if (Enabled && _configurationStatus.IncomingUpdateState.WafKeysToApply.Any())
                {
                    updateResult = _waf?.UpdateWafFromConfigurationStatus(_configurationStatus);
                    if (updateResult?.Success ?? false)
                    {
                        if (!string.IsNullOrEmpty(updateResult.RuleFileVersion))
                        {
                            WafRuleFileVersion = updateResult.RuleFileVersion;
                        }

                        _configurationStatus.ResetUpdateMarkers();
                    }
                }
            }
            catch (Exception e)
            {
                rcmUpdateError = e.Message;
                Log.Warning(e, "An error happened on the rcm subscription callback in class Security");
            }

            var applyDetails = new List<ApplyDetails>();
            var finalError = rcmUpdateError ?? updateResult?.ErrorMessage;
            if (string.IsNullOrEmpty(finalError))
            {
                foreach (var config in configsByProduct.Values.SelectMany(v => v))
                {
                    applyDetails.Add(ApplyDetails.FromOk(config.Path.Path));
                }
            }
            else
            {
                foreach (var config in configsByProduct.Values.SelectMany(v => v))
                {
                    applyDetails.Add(ApplyDetails.FromError(config.Path.Path, finalError));
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

        /// <summary> Frees resources </summary>
        public void Dispose()
        {
            _waf?.Dispose();
            Encoder.Pool.Dispose();
        }

        internal void SetDebugEnabled(bool enabled)
        {
            _wafLibraryInvoker?.SetupLogging(enabled);
        }

        private void SetRemoteConfigCapabilites()
        {
            var rcm = RcmSubscriptionManager.Instance;

            rcm.SetCapability(RcmCapabilitiesIndices.AsmActivation, _settings.CanBeToggled);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmDdRules, _noLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmIpBlocking, _noLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmUserBlocking, _noLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmExclusion, _noLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmRequestBlocking, _noLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmResponseBlocking, _noLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmCustomRules, _noLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmCustomBlockingResponse, _noLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmTrustedIps, _noLocalRules);
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
                    InitializationError = "Error initializing native library";
                    // logs happened during the process of initializing
                    return;
                }

                _wafLibraryInvoker = _libraryInitializationResult.WafLibraryInvoker;
            }

            _wafInitResult = Waf.Waf.Create(_wafLibraryInvoker!, _settings.ObfuscationParameterKeyRegex, _settings.ObfuscationParameterValueRegex, _settings.Rules, _configurationStatus.RulesByFile.Values.FirstOrDefault()?.All, setupWafSchemaExtraction: _settings.ApiSecurityEnabled);
            if (_wafInitResult.Success)
            {
                // we don't reapply configurations to the waf here because it's all done in the subscription function, as new data might have been received at the same time as the enable command, we don't want to update twice (here and in the subscription)
                WafRuleFileVersion = _wafInitResult.RuleFileVersion;
                var oldWaf = _waf;
                _waf = _wafInitResult.Waf;
                oldWaf?.Dispose();
                Log.Debug("Disposed old waf and affected new waf");
                SubscribeToChanges(RcmProducts.AsmData, RcmProducts.Asm);
                Instrumentation.EnableTracerInstrumentations(InstrumentationCategory.AppSec);
                _rateLimiter ??= new(_settings.TraceRateLimit);
                Enabled = true;
                InitializationError = null;
                Log.Information("AppSec is now Enabled, _settings.Enabled is {EnabledValue}, coming from remote config: {EnableFromRemoteConfig}", _settings.Enabled, fromRemoteConfig);
                if (_wafInitResult.EmbeddedRules != null)
                {
                    _configurationStatus.FallbackEmbeddedRuleSet ??= RuleSet.From(_wafInitResult.EmbeddedRules);
                }

                if (!fromRemoteConfig)
                {
                    // occurs the first time we initialize the WAF
                    TelemetryFactory.Metrics.SetWafVersion(_waf!.Version);
                    TelemetryFactory.Metrics.RecordCountWafInit();
                }
            }
            else
            {
                _wafInitResult.Waf?.Dispose();
                Enabled = false;
                InitializationError = "Error initializing waf";
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
                if (_rcmSubscription != null)
                {
                    var newKeys = _rcmSubscription.ProductKeys.Except(new[] { RcmProducts.AsmData, RcmProducts.Asm }).ToArray();
                    if (newKeys.Length > 0)
                    {
                        var newSubscription = new Subscription(UpdateFromRcm, newKeys);
                        _rcmSubscriptionManager.Replace(_rcmSubscription, newSubscription);
                        _rcmSubscription = newSubscription;
                    }
                    else
                    {
                        _rcmSubscriptionManager.Unsubscribe(_rcmSubscription);
                        _rcmSubscription = null;
                    }
                }

                Instrumentation.DisableTracerInstrumentations(InstrumentationCategory.AppSec);
                Enabled = false;
                InitializationError = null;
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
                _rcmSubscriptionManager.Unsubscribe(_rcmSubscription);
            }

            Dispose();
        }
    }
}
