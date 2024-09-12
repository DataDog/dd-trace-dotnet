// <copyright file="Security.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.AsmDd;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
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
        private readonly ConfigurationStatus _configurationStatus;
        private readonly bool _noLocalRules;
        private readonly IRcmSubscriptionManager _rcmSubscriptionManager;
        private ISubscription? _rcmSubscription;
        private LibraryInitializationResult? _libraryInitializationResult;
        private IWaf? _waf;
        private WafLibraryInvoker? _wafLibraryInvoker;
        private AppSecRateLimiter? _rateLimiter;
        private InitResult? _wafInitResult;
        private IDiscoveryService? _discoveryService;
        private bool _spanMetaStructs;
        private string? _blockedHtmlTemplateCache;
        private string? _blockedJsonTemplateCache;
        private HashSet<string>? _activeAddresses;

        /// <summary>
        /// Initializes a new instance of the <see cref="Security"/> class with default settings.
        /// </summary>
        public Security(SecuritySettings? settings = null, IWaf? waf = null, IRcmSubscriptionManager? rcmSubscriptionManager = null)
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
                if (_settings.CanBeToggled || _settings.Enabled)
                {
                    subscriptionsKeys.Add(RcmProducts.AsmFeatures);
                }

                if ((_settings.Enabled || _settings.CanBeToggled) && _noLocalRules)
                {
                    subscriptionsKeys.Add(RcmProducts.AsmDd);
                }

                SubscribeToChanges(subscriptionsKeys.ToArray());

                SetRemoteConfigCapabilites();
                UpdateActiveAddresses();
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

        internal bool RaspEnabled => _settings.RaspEnabled && Enabled;

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

        internal bool IsTrackUserEventsEnabled =>
            Enabled && CalculateIsTrackUserEventsEnabled(_configurationStatus.AutoUserInstrumMode, Settings.UserEventsAutoInstrumentationMode);

        internal bool IsAnonUserTrackingMode => CalculateIsAnonUserTrackingMode(_configurationStatus.AutoUserInstrumMode, Settings.UserEventsAutoInstrumentationMode);

        internal ApiSecurity ApiSecurity { get; }

        internal static bool CalculateIsTrackUserEventsEnabled(string? remote, string local)
        {
            if (remote is SecuritySettings.UserTrackingIdentMode or SecuritySettings.UserTrackingAnonMode)
            {
                return true;
            }

            if (remote is SecuritySettings.UserTrackingDisabled or not null)
            {
                return false;
            }

            // local can never be null, we handle the default in the setting class (so it will be recorded by telemetry)
            return local is SecuritySettings.UserTrackingIdentMode or SecuritySettings.UserTrackingAnonMode;
        }

        internal static bool CalculateIsAnonUserTrackingMode(string? remote, string local)
        {
            if (remote != null)
            {
                return remote is SecuritySettings.UserTrackingAnonMode;
            }

            return local == SecuritySettings.UserTrackingAnonMode;
        }

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

        internal ApplyDetails[] UpdateFromRcmForTest(Dictionary<string, List<RemoteConfiguration>> configsByProduct)
        {
            return UpdateFromRcm(configsByProduct, null);
        }

        private ApplyDetails[] UpdateFromRcm(Dictionary<string, List<RemoteConfiguration>> configsByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigs)
        {
            string? rcmUpdateError = null;
            UpdateResult? updateResult = null;

            try
            {
                // store the last config state, clearing any previous state, without deserializing any payloads yet.
                var anyChange = _configurationStatus.StoreLastConfigState(configsByProduct, removedConfigs);
                var securityStateChange = Enabled != _configurationStatus.EnableAsm;

                // normally CanBeToggled should not need a check as asm_features capacity is only sent if AppSec env var is null, but still guards it in case
                if (securityStateChange && _settings.CanBeToggled)
                {
                    // disable ASM scenario
                    if (Enabled && _configurationStatus.EnableAsm == false)
                    {
                        DisposeWafAndInstrumentations(true);
                    } // enable ASM scenario taking into account rcm changes for other products/data
                    else if (!Enabled && _configurationStatus.EnableAsm == true)
                    {
                        _configurationStatus.ApplyStoredFiles();
                        InitWafAndInstrumentations(true);
                        UpdateActiveAddresses();
                        rcmUpdateError = _wafInitResult?.ErrorMessage;
                        if (_wafInitResult?.RuleFileVersion is not null)
                        {
                            WafRuleFileVersion = _wafInitResult.RuleFileVersion;
                        }
                    }
                } // update asm configuration
                else if (Enabled && anyChange)
                {
                    _configurationStatus.ApplyStoredFiles();
                    updateResult = _waf?.UpdateWafFromConfigurationStatus(_configurationStatus);
                    if (updateResult?.Success ?? false)
                    {
                        if (!string.IsNullOrEmpty(updateResult.RuleFileVersion))
                        {
                            WafRuleFileVersion = updateResult.RuleFileVersion;
                        }

                        _configurationStatus.ResetUpdateMarkers();
                        UpdateActiveAddresses();
                    }
                }
            }
            catch (Exception e)
            {
                rcmUpdateError = e.Message;
                Log.Warning(e, "An error happened on the rcm subscription callback in class Security");
            }

            int productsCount = 0;

            foreach (var config in configsByProduct)
            {
                productsCount += config.Value.Count;
            }

            bool onlyUnknownMatcherErrors = string.IsNullOrEmpty(rcmUpdateError) && HasOnlyUnknownMatcherErrors(updateResult?.Errors);
            var applyDetails = new ApplyDetails[productsCount];
            var finalError = rcmUpdateError ?? updateResult?.ErrorMessage;

            int index = 0;

            if (string.IsNullOrEmpty(finalError) || onlyUnknownMatcherErrors)
            {
                foreach (var config in configsByProduct.Values.SelectMany(v => v))
                {
                    applyDetails[index++] = ApplyDetails.FromOk(config.Path.Path);
                }
            }
            else
            {
                foreach (var config in configsByProduct.Values.SelectMany(v => v))
                {
                    applyDetails[index++] = ApplyDetails.FromError(config.Path.Path, finalError);
                }
            }

            return applyDetails;
        }

        internal static bool HasOnlyUnknownMatcherErrors(IReadOnlyDictionary<string, object>? errors)
        {
            if (errors is not null && errors.Count > 0)
            {
                // if all the errors start with "unknown matcher:", we should not report the error
                // It will happen if the WAF version used does not support new operators defined in the rules
                foreach (var error in errors)
                {
                    if (!error.Key.ToLower().StartsWith("unknown matcher:", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        internal BlockingAction GetBlockingAction(string[]? requestAcceptHeaders, Dictionary<string, object?>? blockInfo, Dictionary<string, object?>? redirectInfo)
        {
            var blockingAction = new BlockingAction();

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
                blockingAction.ResponseContent = GetJsonResponse();
            }

            void SetHtmlResponseContent()
            {
                blockingAction.ContentType = AspNet.MimeTypes.TextHtml;
                blockingAction.ResponseContent = GetHtmlResponse();
            }

            int GetStatusCode(Dictionary<string, object?> information, int defaultValue)
            {
                information.TryGetValue("status_code", out var actionStatusCode);

                if (actionStatusCode is string statusCodeString && int.TryParse(statusCodeString, out var statusCode))
                {
                    return statusCode;
                }
                else
                {
                    Log.Warning("Received a custom block action with an invalid status code {StatusCode}.", actionStatusCode?.ToString());
                    return defaultValue;
                }
            }

            // This should never happen
            if (blockInfo is null && redirectInfo is null)
            {
                Log.Warning("No blockInfo or RedirectInfo found");
                SetAutomaticResponseContent();
                blockingAction.StatusCode = 403;
            }
            else
            {
                if (blockInfo is not null)
                {
                    blockInfo.TryGetValue("type", out var type);

                    switch (type)
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

                        default:
                            Log.Warning("Received a custom block action of invalid type {Type}, an automatic response will be set", type?.ToString());
                            SetAutomaticResponseContent();
                            break;
                    }

                    blockingAction.StatusCode = GetStatusCode(blockInfo, 403);
                }
                else
                {
                    redirectInfo!.TryGetValue("location", out var location);

                    if (location is string locationString && locationString != string.Empty)
                    {
                        var statusCode = GetStatusCode(redirectInfo, 303);
                        blockingAction.StatusCode = statusCode is >= 300 and < 400 ? statusCode : 303;
                        blockingAction.RedirectLocation = locationString;
                        blockingAction.IsRedirect = true;
                    }
                    else
                    {
                        Log.Warning("Received a custom block action of type redirect with null or empty location, an automatic response will be set");
                        SetAutomaticResponseContent();
                        blockingAction.StatusCode = 403;
                    }
                }
            }

            return blockingAction;
        }

        private string GetJsonResponse()
        {
            if (_blockedJsonTemplateCache != null)
            {
                return _blockedJsonTemplateCache;
            }

            _blockedJsonTemplateCache = GetFileTemplate(_settings.BlockedJsonTemplatePath);

            if (_blockedJsonTemplateCache == null)
            {
                _blockedJsonTemplateCache = SecurityConstants.BlockedJsonTemplate;
            }

            return _blockedJsonTemplateCache;
        }

        private string GetHtmlResponse()
        {
            if (_blockedHtmlTemplateCache != null)
            {
                return _blockedHtmlTemplateCache;
            }

            _blockedHtmlTemplateCache = GetFileTemplate(_settings.BlockedHtmlTemplatePath);

            if (_blockedHtmlTemplateCache == null)
            {
                _blockedHtmlTemplateCache = SecurityConstants.BlockedHtmlTemplate;
            }

            return _blockedHtmlTemplateCache;
        }

        private string? GetFileTemplate(string? templatePath)
        {
            try
            {
                var rootDir = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
                if (templatePath != null)
                {
                    var fullPath =
                        Path.IsPathRooted(templatePath)
                            ? templatePath
                            : Path.Combine(rootDir, templatePath);

                    if (File.Exists(fullPath))
                    {
                        return File.ReadAllText(fullPath);
                    }

                    Log.Warning("Response template doesn't exist, templatePath: {TemplatePath} {FullPath}", templatePath, fullPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error setting blocking template, will default to built in template, templatePath: {TemplatePath}", templatePath);
            }

            return null;
        }

        /// <summary> Frees resources </summary>
        public void Dispose()
        {
            _waf?.Dispose();
            Encoder.Pool.Dispose();
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
            rcm.SetCapability(RcmCapabilitiesIndices.AsmRaspLfi, _settings.RaspEnabled && _noLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmRaspLfi));
            rcm.SetCapability(RcmCapabilitiesIndices.AsmRaspSsrf, _settings.RaspEnabled && _noLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmRaspSsrf));
            rcm.SetCapability(RcmCapabilitiesIndices.AsmRaspShi, _settings.RaspEnabled && _noLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmRaspShi));
            rcm.SetCapability(RcmCapabilitiesIndices.AsmRaspSqli, _settings.RaspEnabled && _noLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmRaspSqli));
            rcm.SetCapability(RcmCapabilitiesIndices.AsmExclusionData, _noLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmExclusionData));
            // follows a different pattern to rest of ASM remote config, if available it's the RC value
            // that takes precedence. This follows what other products do.
            rcm.SetCapability(RcmCapabilitiesIndices.AsmAutoUserInstrumentationMode, true);
        }

        private bool WafSupportsCapability(BigInteger capability)
        {
            return RCMCapabilitiesHelper.WafSupportsCapability(capability, _waf?.Version);
        }

        private void InitWafAndInstrumentations(bool configurationFromRcm = false)
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

            _wafInitResult = Waf.Waf.Create(
                _wafLibraryInvoker!,
                _settings.ObfuscationParameterKeyRegex,
                _settings.ObfuscationParameterValueRegex,
                _settings.Rules,
                configurationFromRcm ? _configurationStatus : null,
                _settings.UseUnsafeEncoder,
                GlobalSettings.Instance.DebugEnabledInternal && _settings.WafDebugEnabled);
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
                Log.Information("AppSec is now Enabled, _settings.Enabled is {EnabledValue}, coming from remote config: {EnableFromRemoteConfig}", _settings.Enabled, configurationFromRcm);
                if (_wafInitResult.EmbeddedRules != null)
                {
                    _configurationStatus.FallbackEmbeddedRuleSet ??= RuleSet.From(_wafInitResult.EmbeddedRules);
                }

                if (!configurationFromRcm)
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
                span.Context.TraceContext?.Tags.SetTag(Tags.Propagated.AppSec, "1");
            }
        }

        internal IContext? CreateAdditiveContext() => _waf?.CreateContext();

        private void RunShutdown(Exception? ex)
        {
            if (_rcmSubscription != null)
            {
                _rcmSubscriptionManager.Unsubscribe(_rcmSubscription);
            }

            Dispose();
        }

        internal bool IsMetaStructSupported()
        {
            if (_discoveryService is null)
            {
                _discoveryService = Tracer.Instance.TracerManager.DiscoveryService;
                _discoveryService?.SubscribeToChanges(config => _spanMetaStructs = config.SpanMetaStructs);
            }

            return _spanMetaStructs;
        }

        internal void UpdateActiveAddresses()
        {
            // So far, RASP is the only one that uses this
            if (_settings.RaspEnabled && _waf?.IsKnowAddressesSuported() == true)
            {
                var addresses = _waf.GetKnownAddresses();
                Log.Debug("Updating WAF active addresses to {Addresses}", addresses);
                _activeAddresses = addresses is null ? null : new HashSet<string>(addresses);
            }
            else
            {
                _activeAddresses = null;
            }
        }

        internal bool AddressEnabled(string address)
        {
            // So far, RASP is the only one that uses this
            if (!_settings.RaspEnabled)
            {
                return false;
            }

            if (_waf?.IsKnowAddressesSuported() == true)
            {
                return _activeAddresses?.Contains(address) ?? false;
            }
            else
            {
                // If we don't support knowAddresses, we will have to call the WAF
                return true;
            }
        }
    }
}
