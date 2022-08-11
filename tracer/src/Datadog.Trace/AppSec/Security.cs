// <copyright file="Security.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.AppSec.Transports;
using Datadog.Trace.AppSec.Transports.Http;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.AppSec
{
    /// <summary>
    /// The Secure is responsible coordinating ASM
    /// </summary>
    internal class Security : IDatadogSecurity, IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Security>();

        private static readonly Dictionary<string, string> RequestHeaders;
        private static readonly Dictionary<string, string> ResponseHeaders;

        private static Security _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();

        private readonly InstrumentationGateway _instrumentationGateway;
        private readonly SecuritySettings _settings;
        private IWaf _waf;
        private AppSecRateLimiter _rateLimiter;
        private bool _enabled = false;
        private IDictionary<string, Payload> _asmDataConfigs;

        private bool? _usingIntegratedPipeline = null;

        static Security()
        {
            RequestHeaders = new()
            {
                { "X-FORWARDED-FOR", string.Empty },
                { "X-CLIENT-IP", string.Empty },
                { "X-REAL-IP", string.Empty },
                { "X-FORWARDED", string.Empty },
                { "X-CLUSTER-CLIENT-IP", string.Empty },
                { "FORWARDED-FOR", string.Empty },
                { "FORWARDED", string.Empty },
                { "VIA", string.Empty },
                { "TRUE-CLIENT-IP", string.Empty },
                { "Content-Length", string.Empty },
                { "Content-Type", string.Empty },
                { "Content-Encoding", string.Empty },
                { "Content-Language", string.Empty },
                { "Host", string.Empty },
                { "user-agent", string.Empty },
                { "Accept", string.Empty },
                { "Accept-Encoding", string.Empty },
                { "Accept-Language", string.Empty },
            };

            ResponseHeaders = new()
            {
                { "content-length", string.Empty }, { "content-type", string.Empty }, { "Content-Encoding", string.Empty }, { "Content-Language", string.Empty },
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Security"/> class with default settings.
        /// </summary>
        public Security()
            : this(null, null)
        {
        }

        private Security(SecuritySettings settings = null, InstrumentationGateway instrumentationGateway = null, IWaf waf = null)
        {
            try
            {
                _settings = settings ?? SecuritySettings.FromDefaultSources();
                _instrumentationGateway = instrumentationGateway ?? new InstrumentationGateway();
                _waf = waf;
                LifetimeManager.Instance.AddShutdownTask(RunShutdown);

                if (_settings.CanBeEnabled)
                {
                    UpdateStatus();
                    AsmRemoteConfigurationProducts.AsmFeaturesProduct.ConfigChanged += FeaturesProductConfigChanged;
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

        /// <summary>
        /// Gets <see cref="InstrumentationGateway"/> instance
        /// </summary>
        InstrumentationGateway IDatadogSecurity.InstrumentationGateway => _instrumentationGateway;

        internal InstrumentationGateway InstrumentationGateway => _instrumentationGateway;

        internal bool WafExportsErrorHappened => _waf?.InitializationResult?.ExportErrors ?? false;

        /// <summary>
        /// Gets <see cref="SecuritySettings"/> instance
        /// </summary>
        SecuritySettings IDatadogSecurity.Settings => _settings;

        internal SecuritySettings Settings => _settings;

        internal string DdlibWafVersion => _waf?.Version;

        private static void AnnotateSpan(Span span)
        {
            // we should only tag service entry span, the first span opened for a
            // service. For WAF it's safe to assume we always have service entry spans
            // we'll need to revisit this for RASP.
            if (span != null)
            {
                span.SetMetric(Metrics.AppSecEnabled, 1.0);
                span.SetTag(Tags.RuntimeFamily, TracerConstants.Language);
            }
        }

        private static void LogMatchesIfDebugEnabled(string result, bool blocked)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                var results = JsonConvert.DeserializeObject<WafMatch[]>(result);
                for (var i = 0; i < results.Length; i++)
                {
                    var match = results[i];
                    Log.Debug(blocked ? "DDAS-0012-02: Blocking current transaction (rule: {RuleId})" : "DDAS-0012-01: Detecting an attack from rule {RuleId}", match.Rule);
                }
            }
        }

        private static void AddHeaderTags(Span span, IHeadersCollection headers, Dictionary<string, string> headersToCollect, string prefix)
        {
            var tags = SpanContextPropagator.Instance.ExtractHeaderTags(headers, headersToCollect, defaultTagPrefix: prefix);
            foreach (var tag in tags)
            {
                span.SetTag(tag.Key, tag.Value);
            }
        }

        private static Span GetLocalRootSpan(Span span)
        {
            var localRootSpan = span.Context.TraceContext?.RootSpan;
            return localRootSpan ?? span;
        }

        private static void TryAddEndPoint(Span span)
        {
            var route = span.GetTag(Tags.AspNetCoreRoute) ?? span.GetTag(Tags.AspNetRoute);
            if (route != null)
            {
                span.SetTag(Tags.HttpEndpoint, route);
            }
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
                Log.Error(ex, ex.Message);
            }
        }

        private static void RemoveAppsecSpecificInstrumentations()
        {
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
        public void Dispose()
        {
            _waf?.Dispose();
        }

        private void SetRemoteConfigCapabilites()
        {
            RemoteConfigurationManager.CallbackWithInitializedInstance(
                rcm =>
                {
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmActivation, _settings.CanBeEnabled);
                    // TODO set to '_settings.Rules == null' when https://github.com/DataDog/dd-trace-dotnet/pull/3120 is merged
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmDdRules, false);
                    rcm.SetCapability(RcmCapabilitiesIndices.AsmIpBlocking, true);
                });
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

            var updated = _waf.UpdateRules(_asmDataConfigs.SelectMany(p => p.Value.RulesData));
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

        private void UpdateStatus(bool fromRemoteConfig = false)
        {
            if (_enabled == _settings.Enabled) { return; }

            lock (_settings)
            {
                if (_settings.Enabled)
                {
                    _waf?.Dispose();

                    _waf = Waf.Waf.Create(_settings.ObfuscationParameterKeyRegex, _settings.ObfuscationParameterValueRegex, _settings.Rules);
                    if (_waf?.InitializedSuccessfully ?? false)
                    {
                        EnableWaf(fromRemoteConfig);
                    }
                    else
                    {
                        _settings.Enabled = false;
                    }
                }

                if (!_settings.Enabled)
                {
                    DisableWaf(fromRemoteConfig);
                }
            }
        }

        private void EnableWaf(bool fromRemoteConfig)
        {
            if (!_enabled)
            {
                AsmRemoteConfigurationProducts.AsmDataProduct.ConfigChanged += AsmDataProductConfigChanged;
                // reapply rules if existing ones in memory
                _waf.UpdateRules(_asmDataConfigs?.SelectMany(p => p.Value.RulesData));
                _instrumentationGateway.StartRequest += RunWafAndReact;
                _instrumentationGateway.EndRequest += RunWafAndReactAndCleanup;
                _instrumentationGateway.PathParamsAvailable += RunWafAndReact;
                _instrumentationGateway.BodyAvailable += RunWafAndReact;
                _instrumentationGateway.BlockingOpportunity += MightStopRequest;
#if NETFRAMEWORK
                if (_usingIntegratedPipeline == null)
                {
                    try
                    {
                        _usingIntegratedPipeline = TryGetUsingIntegratedPipelineBool();
                    }
                    catch (Exception ex)
                    {
                        _usingIntegratedPipeline = false;
                        Log.Error(ex, "Unable to query the IIS pipeline. Request and response information may be limited.");
                    }
                }

                if (_usingIntegratedPipeline.Value)
                {
                    _instrumentationGateway.LastChanceToWriteTags += InstrumentationGateway_AddHeadersResponseTags;
                }

#else
                _instrumentationGateway.LastChanceToWriteTags += InstrumentationGateway_AddHeadersResponseTags;
#endif
                AddAppsecSpecificInstrumentations();

                _instrumentationGateway.StartRequest += ReportWafInitInfoOnce;
                _rateLimiter ??= new AppSecRateLimiter(_settings.TraceRateLimit);

                _enabled = true;

                Log.Information("AppSec is now Enabled, _settings.Enabled is {EnabledValue}, coming from remote config: {enableFromRemoteConfig}", _settings.Enabled, fromRemoteConfig);
            }
        }

        private void DisableWaf(bool fromRemoteConfig)
        {
            if (_enabled)
            {
                _instrumentationGateway.StartRequest -= RunWafAndReact;
                _instrumentationGateway.EndRequest -= RunWafAndReactAndCleanup;
                _instrumentationGateway.PathParamsAvailable -= RunWafAndReact;
                _instrumentationGateway.BodyAvailable -= RunWafAndReact;
                _instrumentationGateway.BlockingOpportunity -= MightStopRequest;
                _instrumentationGateway.LastChanceToWriteTags -= InstrumentationGateway_AddHeadersResponseTags;
                _instrumentationGateway.StartRequest -= ReportWafInitInfoOnce;

                AsmRemoteConfigurationProducts.AsmDataProduct.ConfigChanged -= AsmDataProductConfigChanged;
                RemoveAppsecSpecificInstrumentations();

                _enabled = false;

                Log.Information("AppSec is now Disabled, _settings.Enabled is {EnabledValue}, coming from remote config: {enableFromRemoteConfig}", _settings.Enabled, fromRemoteConfig);
            }
        }

        private void UpdateStatus()
        {
            lock (_settings)
            {
                if (_enabled == _settings.Enabled) { return; }
                if (_settings.Enabled)
                {
                    _waf = _waf ?? Waf.Waf.Create(_settings.ObfuscationParameterKeyRegex, _settings.ObfuscationParameterValueRegex, _settings.Rules);
                    if (_waf?.InitializedSuccessfully ?? false)
                    {
                        _instrumentationGateway.EndRequest += RunWaf;
                        _instrumentationGateway.PathParamsAvailable += AggregateAddressesInContext;
                        _instrumentationGateway.BodyAvailable += AggregateAddressesInContext;
#if NETFRAMEWORK
                        try
                        {
                            _usingIntegratedPipeline = TryGetUsingIntegratedPipelineBool();
                        }
                        catch (Exception ex)
                        {
                            _usingIntegratedPipeline = false;
                            Log.Error(ex, "Unable to query the IIS pipeline. Request and response information may be limited.");
                        }

                        if (_usingIntegratedPipeline)
                        {
                            _instrumentationGateway.LastChanceToWriteTags += InstrumentationGateway_AddHeadersResponseTags;
                        }
#else
                        _instrumentationGateway.LastChanceToWriteTags += InstrumentationGateway_AddHeadersResponseTags;
#endif
                        AddAppsecSpecificInstrumentations();

                        _instrumentationGateway.EndRequest += ReportWafInitInfoOnce;
                        LifetimeManager.Instance.AddShutdownTask(RunShutdown);
                        _rateLimiter = _rateLimiter ?? new AppSecRateLimiter(_settings.TraceRateLimit);
                    }
                    else
                    {
                        _settings.Enabled = false;
                    }
                }

                if (!_settings.Enabled)
                {
                    _instrumentationGateway.EndRequest -= RunWaf;
                    _instrumentationGateway.PathParamsAvailable -= AggregateAddressesInContext;
                    _instrumentationGateway.BodyAvailable -= AggregateAddressesInContext;
                    _instrumentationGateway.LastChanceToWriteTags -= InstrumentationGateway_AddHeadersResponseTags;
                    _instrumentationGateway.EndRequest -= ReportWafInitInfoOnce;

                    AddAppsecSpecificInstrumentations(false);
                }

                _enabled = _settings.Enabled;
            }
        }

        private void InstrumentationGateway_AddHeadersResponseTags(object sender, InstrumentationGatewayEventArgs e)
        {
            if (e.RelatedSpan.GetTag(Tags.AppSecEvent) == "true")
            {
                AddResponseHeaderTags(e.Transport, e.RelatedSpan);
            }
        }

        private void Report(ITransport transport, Span span, IResult result, bool blocked)
        {
            span.SetTag(Tags.AppSecEvent, "true");
            if (blocked)
            {
                span.SetTag(Tags.AppSecBlocked, "true");
            }

            var resultData = result.Data;
            SetTraceSamplingPriority(span);

            LogMatchesIfDebugEnabled(resultData, blocked);

            span.SetTag(Tags.AppSecJson, "{\"triggers\":" + resultData + "}");
            var clientIp = span.GetTag(Tags.HttpClientIp);
            if (!string.IsNullOrEmpty(clientIp))
            {
                span.SetTag(Tags.ActorIp, clientIp);
            }

            var origin = span.GetTag(Tags.Origin);
            if (origin == null)
            {
                span.SetTag(Tags.Origin, "appsec");
            }

            span.SetTag(Tags.AppSecRuleFileVersion, _waf.InitializationResult.RuleFileVersion);
            span.SetMetric(Metrics.AppSecWafDuration, result.AggregatedTotalRuntime);
            span.SetMetric(Metrics.AppSecWafAndBindingsDuration, result.AggregatedTotalRuntimeWithBindings);
            var headers = transport.GetRequestHeaders();
            AddHeaderTags(span, headers, RequestHeaders, SpanContextPropagator.HttpRequestHeadersTagPrefix);
        }

        private void SetTraceSamplingPriority(Span span)
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

        private bool CanAccessHeaders()
        {
            return _usingIntegratedPipeline == true || _usingIntegratedPipeline is null;
        }

        private void AddResponseHeaderTags(ITransport transport, Span span)
        {
            TryAddEndPoint(span);
            var headers =
                CanAccessHeaders() ?
                    transport.GetResponseHeaders() :
                    new NameValueHeadersCollection(new NameValueCollection());
            AddHeaderTags(span, headers, ResponseHeaders, SpanContextPropagator.HttpResponseHeadersTagPrefix);
        }

        private IContext GetOrCreateContext(ITransport transport)
        {
            var additiveContext = transport.GetAdditiveContext();

            if (additiveContext == null)
            {
                additiveContext = _waf.CreateContext();
                transport.SetAdditiveContext(additiveContext);
            }

            return additiveContext;
        }

        private void MightStopRequest(object sender, InstrumentationGatewayBlockingEventArgs args)
        {
            if (args.Transport.Blocked)
            {
                AddResponseHeaderTags(args.Transport, args.Scope.Span);
                args.InvokeDoBeforeBlocking();
                var transport = args.Transport;
                var additiveContext = GetOrCreateContext(transport);
                additiveContext.Dispose();
                throw new BlockException();
            }
        }

        private void RunWafAndReactAndCleanup(object sender, InstrumentationGatewaySecurityEventArgs e)
        {
            RunWafAndReact(sender, e);
            e.Transport.DisposeAdditiveContext();
        }

        private void RunWafAndReact(object sender, InstrumentationGatewaySecurityEventArgs e)
        {
            try
            {
                if (e.Transport.Blocked)
                {
                    return;
                }

                var additiveContext = GetOrCreateContext(e.Transport);
                var span = GetLocalRootSpan(e.RelatedSpan);

                AnnotateSpan(span);

                // run the WAF and execute the results
                using var wafResult = additiveContext.Run(e.EventData, _settings.WafTimeoutMicroSeconds);
                if (wafResult.ReturnCode is ReturnCode.Match or ReturnCode.Block)
                {
                    var block = wafResult.ReturnCode == ReturnCode.Block || wafResult.Data.Contains("ublock") || wafResult.Actions.Contains("block");
                    if (block)
                    {
                        e.Transport.WriteBlockedResponse(_settings.BlockedJsonTemplate, _settings.BlockedHtmlTemplate, CanAccessHeaders());
                    }

                    Report(e.Transport, span, wafResult, block);
                }
            }
            catch (Exception ex) when (ex is not BlockException)
            {
                Log.Error(ex, "Call into the security module failed");
            }
        }

        public void ProcessControlCommand(object arg)
        {
#if !NETFRAMEWORK
            if (arg.TryDuckCast<DiagnosticListeners.AspNetCoreDiagnosticObserver.HttpRequestInStartStruct>(out var requestStruct))
            {
                var httpContext = requestStruct.HttpContext;
                var request = httpContext.Request;
                if (request.Path == "/EnableASM")
                {
                    _settings.Enabled = true;
                    UpdateStatus();
                    return;
                }

                if (request.Path == "/DisableASM")
                {
                    _settings.Enabled = false;
                    UpdateStatus();
                    return;
                }
            }
#endif
        }

        private void ReportWafInitInfoOnce(object sender, InstrumentationGatewaySecurityEventArgs e)
        {
            _instrumentationGateway.StartRequest -= ReportWafInitInfoOnce;
            var span = e.RelatedSpan.Context.TraceContext.RootSpan ?? e.RelatedSpan;
            span.Context.TraceContext?.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Asm);
            span.SetMetric(Metrics.AppSecWafInitRulesLoaded, _waf.InitializationResult.LoadedRules);
            span.SetMetric(Metrics.AppSecWafInitRulesErrorCount, _waf.InitializationResult.FailedToLoadRules);
            if (_waf.InitializationResult.HasErrors)
            {
                span.SetTag(Tags.AppSecWafInitRuleErrors, _waf.InitializationResult.ErrorMessage);
            }

            span.SetTag(Tags.AppSecWafVersion, _waf.Version);
        }

        private void RunShutdown()
        {
            if (_instrumentationGateway != null)
            {
                _instrumentationGateway.PathParamsAvailable -= RunWafAndReact;
                _instrumentationGateway.BodyAvailable -= RunWafAndReact;
                _instrumentationGateway.StartRequest -= RunWafAndReact;
                _instrumentationGateway.EndRequest -= RunWafAndReactAndCleanup;
                _instrumentationGateway.BlockingOpportunity -= MightStopRequest;
                _instrumentationGateway.LastChanceToWriteTags -= InstrumentationGateway_AddHeadersResponseTags;
            }

            AsmRemoteConfigurationProducts.AsmDataProduct.ConfigChanged -= AsmDataProductConfigChanged;
            AsmRemoteConfigurationProducts.AsmFeaturesProduct.ConfigChanged -= FeaturesProductConfigChanged;
            Dispose();
        }

#if NETFRAMEWORK
        /// <summary>
        /// ! This method should be called from within a try-catch block !
        /// If the application is running in partial trust, then trying to call this method will result in
        /// a SecurityException to be thrown at the method CALLSITE, not inside the <c>TryGetUsingIntegratedPipelineBool(..)</c> method itself.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetUsingIntegratedPipelineBool() => System.Web.HttpRuntime.UsingIntegratedPipeline;
#endif
    }
}
