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
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.AppSec
{
    /// <summary>
    /// The Secure is responsible coordinating ASM
    /// </summary>
    internal sealed class Security : IDatadogSecurity, IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Security>();
        private static Security? _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();
        private readonly SecuritySettings _settings;
        private readonly ConfigurationState _configurationState;
        private readonly IRcmSubscriptionManager _rcmSubscriptionManager;

        /// <summary>
        /// _waf locker needs to have a longer lifecycle than the Waf object as it's used to dispose it as well
        /// </summary>
        private readonly Concurrency.ReaderWriterLock _activeAddressesLocker;
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
        public Security(SecuritySettings? settings = null, IWaf? waf = null, IRcmSubscriptionManager? rcmSubscriptionManager = null, ConfigurationState? configurationState = null)
        {
            _rcmSubscriptionManager = rcmSubscriptionManager ?? RcmSubscriptionManager.Instance;
            _activeAddressesLocker = new Concurrency.ReaderWriterLock();
            var telemetry = TelemetryFactory.Config;

            try
            {
                _settings = settings ?? SecuritySettings.FromDefaultSources();
                _waf = waf;
                _configurationState = configurationState ?? new ConfigurationState(_settings, telemetry, _waf is null);
                LifetimeManager.Instance.AddShutdownTask(RunShutdown);

                if (_configurationState.IncomingUpdateState.ShouldInitAppsec)
                {
                    InitWafAndInstrumentations();
                }
                else
                {
                    Log.Information("AppSec was not activated, its status is enabled={AppSecEnabled}, AppSec can be remotely enabled={CanBeRcEnabled}.", AppsecEnabled, _settings.CanBeToggled);
                }

                RefreshRcmSubscriptions();
                SetRemoteConfigCapabilites();
                UpdateActiveAddresses();
            }
            catch (Exception ex)
            {
                _settings ??= new(source: null, telemetry);
                _configurationState ??= new ConfigurationState(_settings, telemetry, true);
                Log.Error(ex, "DDAS-0001-01: AppSec could not start because of an unexpected error. No security activities will be collected. Please contact support at https://docs.datadoghq.com/help/ for help.");
            }
            finally
            {
                _settings ??= new(source: null, telemetry);
                ApiSecurity = new(_settings);
                _configurationState?.IncomingUpdateState.Dispose();
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

        internal bool AppsecEnabled => _configurationState.AppsecEnabled;

        internal bool RaspEnabled => _settings.RaspEnabled && AppsecEnabled;

        internal string? InitializationError { get; private set; }

        internal bool WafExportsErrorHappened => _libraryInitializationResult is { Status: LibraryInitializationResult.LoadStatus.ExportError };

        internal string? WafRuleFileVersion { get; private set; }

        internal InitResult? WafInitResult => _wafInitResult;

        /// <summary>
        /// Gets <see cref="SecuritySettings"/> instance
        /// </summary>
        SecuritySettings IDatadogSecurity.Settings => _settings;

        internal SecuritySettings Settings => _settings;

        internal string? DdlibWafVersion => _waf?.Version;

        internal bool IsTrackUserEventsEnabled =>
            AppsecEnabled && CalculateIsTrackUserEventsEnabled(_configurationState.AutoUserInstrumMode, Settings.UserEventsAutoInstrumentationMode);

        internal bool IsAnonUserTrackingMode => CalculateIsAnonUserTrackingMode(_configurationState.AutoUserInstrumMode, Settings.UserEventsAutoInstrumentationMode);

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

        /// <summary>
        /// This is cumulative, if a subscription already existed with other product names,  the new ones will be unioned
        /// </summary>
        internal void SubscribeToChanges(params string[] productNames)
        {
            if (_rcmSubscription is not null)
            {
                var newSubscription = new Subscription(UpdateFromRcm, [.. productNames]);
                _rcmSubscriptionManager.Replace(_rcmSubscription, newSubscription);
                _rcmSubscription = newSubscription;
            }
            else
            {
                _rcmSubscription = new Subscription(UpdateFromRcm, [.. productNames]);
                _rcmSubscriptionManager.SubscribeToChanges(_rcmSubscription);
            }
        }

        /// <summary>
        /// This method handles new config files sent from RCM. First we notify configuration state class that a new config is received (files are going to be stored without deserialization first to reduce memory footprint)
        /// Configuration state, given its state and the contents of asm features file (toggling appsec) will take its decision. And we react accordingly, following what it says to do: turning on / off / update the waf
        /// After all treatment, incoming update state is reset
        /// </summary>
        /// <param name="configsByProduct">new configs or updates</param>
        /// <param name="removedConfigs">removed files</param>
        /// <returns>apply details to be sent back to rcm</returns>
        private ApplyDetails[] UpdateFromRcm(Dictionary<string, List<RemoteConfiguration>> configsByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigs)
        {
            string? rcmUpdateError = null;
            UpdateResult? updateResult = null;
            using (_configurationState.IncomingUpdateState)
            {
                try
                {
                    // store the last config state, clearing any previous state, without deserializing any payloads yet.
                    _configurationState.ReceivedNewConfig(configsByProduct, removedConfigs);
                    if (_configurationState.IncomingUpdateState.ShouldDisableAppsec)
                    {
                        // disable ASM scenario
                        DisposeWafAndInstrumentations(true);
                    } // enable ASM scenario taking into account rcm changes for other products/data
                    else if (_configurationState.IncomingUpdateState.ShouldInitAppsec)
                    {
                        InitWafAndInstrumentations();
                        UpdateActiveAddresses();
                        rcmUpdateError = _wafInitResult?.ErrorMessage;
                        if (_wafInitResult?.RuleFileVersion is { Length: > 0 })
                        {
                            WafRuleFileVersion = _wafInitResult.RuleFileVersion;
                            TelemetryFactory.Metrics.SetWafAndRulesVersion(_waf!.Version, WafRuleFileVersion);
                        }

                        RefreshRcmSubscriptions();
                    } // update asm configuration
                    else if (_configurationState.IncomingUpdateState.ShouldUpdateAppsec)
                    {
                        updateResult = _waf?.Update(_configurationState);
                        if (updateResult?.Success ?? false)
                        {
                            if (!string.IsNullOrEmpty(updateResult.RuleFileVersion))
                            {
                                WafRuleFileVersion = updateResult.RuleFileVersion;
                                TelemetryFactory.Metrics.SetWafAndRulesVersion(_waf!.Version, WafRuleFileVersion);
                            }

                            UpdateActiveAddresses();
                        }
                    }
                }
                catch (Exception e)
                {
                    rcmUpdateError = e.Message;
                    Log.Error(e, "An error happened on the rcm subscription callback in class Security");
                }

                var productsCount = 0;

                foreach (var config in configsByProduct)
                {
                    productsCount += config.Value.Count;
                }

                var onlyUnknownMatcherErrors = string.IsNullOrEmpty(rcmUpdateError) && HasOnlyUnknownMatcherErrors(updateResult?.RuleErrors);
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
        }

        private void RefreshRcmSubscriptions()
        {
            var subscriptionKeys = _configurationState.WhatProductsAreRelevant(_settings);
            SubscribeToChanges(subscriptionKeys);
        }

        internal static bool HasOnlyUnknownMatcherErrors(IReadOnlyDictionary<string, object>? errors)
        {
            if (errors is not null && errors.Count > 0)
            {
                // if all the errors start with "unknown matcher:", we should not report the error
                // It will happen if the WAF version used does not support new operators defined in the rules
                foreach (var error in errors)
                {
                    if (!error.Key.ToLower().Contains("unknown matcher:"))
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

                if (actionStatusCode is ulong statusCodeInt)
                {
                    return (int)statusCodeInt;
                }
                else if (actionStatusCode is string statusCodeString && int.TryParse(statusCodeString, out var statusCode))
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
            Encoder.Dispose();
            _activeAddressesLocker.Dispose();
        }

        private void SetRemoteConfigCapabilites()
        {
            var rcm = RcmSubscriptionManager.Instance;

            rcm.SetCapability(RcmCapabilitiesIndices.AsmActivation, _settings.CanBeToggled);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmDdRules, _settings.NoCustomLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmIpBlocking, _settings.NoCustomLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmUserBlocking, _settings.NoCustomLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmExclusion, _settings.NoCustomLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmRequestBlocking, _settings.NoCustomLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmResponseBlocking, _settings.NoCustomLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmCustomRules, _settings.NoCustomLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmCustomBlockingResponse, _settings.NoCustomLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmTrustedIps, _settings.NoCustomLocalRules);
            rcm.SetCapability(RcmCapabilitiesIndices.AsmRaspLfi, _settings.RaspEnabled && _settings.NoCustomLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmRaspLfi));
            rcm.SetCapability(RcmCapabilitiesIndices.AsmRaspSsrf, _settings.RaspEnabled && _settings.NoCustomLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmRaspSsrf));
            rcm.SetCapability(RcmCapabilitiesIndices.AsmRaspShi, _settings.RaspEnabled && _settings.NoCustomLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmRaspShi));
            rcm.SetCapability(RcmCapabilitiesIndices.AsmRaspSqli, _settings.RaspEnabled && _settings.NoCustomLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmRaspSqli));
            rcm.SetCapability(RcmCapabilitiesIndices.AsmRaspCmd, _settings.RaspEnabled && _settings.NoCustomLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmRaspCmd));
            rcm.SetCapability(RcmCapabilitiesIndices.AsmExclusionData, _settings.NoCustomLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmExclusionData));
            rcm.SetCapability(RcmCapabilitiesIndices.AsmEnpointFingerprint, _settings.NoCustomLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmEnpointFingerprint));
            rcm.SetCapability(RcmCapabilitiesIndices.AsmHeaderFingerprint, _settings.NoCustomLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmHeaderFingerprint));
            rcm.SetCapability(RcmCapabilitiesIndices.AsmNetworkFingerprint, _settings.NoCustomLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmNetworkFingerprint));
            rcm.SetCapability(RcmCapabilitiesIndices.AsmSessionFingerprint, _settings.NoCustomLocalRules && WafSupportsCapability(RcmCapabilitiesIndices.AsmSessionFingerprint));
            // follows a different pattern to rest of ASM remote config, if available it's the RC value
            // that takes precedence. This follows what other products do.
            rcm.SetCapability(RcmCapabilitiesIndices.AsmAutoUserInstrumentationMode, true);
        }

        private bool WafSupportsCapability(BigInteger capability)
        {
            return RCMCapabilitiesHelper.WafSupportsCapability(capability, _waf?.Version);
        }

        private void InitWafAndInstrumentations()
        {
            // initialization of WafLibraryInvoker
            if (_libraryInitializationResult == null)
            {
                _libraryInitializationResult = WafLibraryInvoker.Initialize();
                if (!_libraryInitializationResult.Success)
                {
                    _configurationState.AppsecEnabled = false;
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
                _configurationState,
                _settings.UseUnsafeEncoder,
                GlobalSettings.Instance.DebugEnabled && _settings.WafDebugEnabled);
            if (_wafInitResult.Success)
            {
                // we don't reapply configurations to the waf here because it's all done in the subscription function, as new data might have been received at the same time as the enable command, we don't want to update twice (here and in the subscription)
                WafRuleFileVersion = _wafInitResult.RuleFileVersion;
                var oldWaf = _waf;
                _waf = _wafInitResult.Waf;
                if (oldWaf is not null)
                {
                    oldWaf.Dispose();
                    Log.Debug("Disposed old waf and created a new one");
                }

                Instrumentation.EnableTracerInstrumentations(InstrumentationCategory.AppSec);
                _rateLimiter ??= new(_settings.TraceRateLimit);
                _configurationState.AppsecEnabled = true;
                InitializationError = null;
                Log.Information("AppSec is now Enabled, _settings.Enabled is {EnabledValue}, from ruleset: {RuleSet}", _settings.AppsecEnabled, _configurationState.RuleSetTitle);

                if (oldWaf is null)
                {
                    // occurs the first time we initialize the WAF
                    TelemetryFactory.Metrics.SetWafAndRulesVersion(_waf!.Version, WafRuleFileVersion);
                    TelemetryFactory.Metrics.RecordCountWafInit(Telemetry.Metrics.MetricTags.WafStatus.Success);
                }
            }
            else
            {
                TelemetryFactory.Metrics.RecordCountWafInit(Telemetry.Metrics.MetricTags.WafStatus.Error);
                _wafInitResult.Waf?.Dispose();
                _configurationState.AppsecEnabled = false;
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
            if (AppsecEnabled)
            {
                Instrumentation.DisableTracerInstrumentations(InstrumentationCategory.AppSec);
                _configurationState.AppsecEnabled = false;
                RefreshRcmSubscriptions();
                InitializationError = null;
                Log.Information("AppSec is now Disabled, _settings.Enabled is {EnabledValue}, coming from remote config: {EnableFromRemoteConfig}", _settings.AppsecEnabled, fromRemoteConfig);
            }
        }

        internal void SetTraceSamplingPriority(Span span, bool setSource = true)
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
                if (setSource)
                {
                    span.Context.TraceContext?.Tags.EnableTraceSources(TraceSources.Asm);
                }
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

        private void UpdateActiveAddresses()
        {
            if (_waf?.IsKnowAddressesSuported() is true)
            {
                var addresses = _waf.GetKnownAddresses();
                Log.Debug("Updating WAF active addresses to {Addresses}", addresses);
                try
                {
                    _activeAddressesLocker.EnterWriteLock();
                    _activeAddresses = [..addresses];
                }
                finally
                {
                    _activeAddressesLocker.ExitWriteLock();
                }
            }
            else
            {
                try
                {
                    _activeAddressesLocker.EnterWriteLock();
                    _activeAddresses = null;
                }
                finally
                {
                    _activeAddressesLocker.ExitWriteLock();
                }
            }
        }

        public bool AddressEnabled(string address)
        {
            if (_waf?.IsKnowAddressesSuported() == true)
            {
                try
                {
                    _activeAddressesLocker.EnterReadLock();
                    return _activeAddresses?.Contains(address) ?? false;
                }
                finally
                {
                    _activeAddressesLocker.ExitReadLock();
                }
            }

            // If we don't support knowAddresses, we will have to call the WAF
            return true;
        }
    }
}
