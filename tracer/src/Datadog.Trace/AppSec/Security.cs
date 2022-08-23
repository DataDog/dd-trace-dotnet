// <copyright file="Security.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.AppSec.Transports;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

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

        private readonly AppSecRateLimiter _rateLimiter;
        private readonly IWaf _waf;
        private readonly InstrumentationGateway _instrumentationGateway;
        private readonly SecuritySettings _settings;

#if NETFRAMEWORK
        private readonly bool _usingIntegratedPipeline;
#endif

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
                { "content-length", string.Empty },
                { "content-type", string.Empty },
                { "Content-Encoding", string.Empty },
                { "Content-Language", string.Empty },
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

                if (_settings.Enabled)
                {
                    _waf = waf ?? Waf.Waf.Create(_settings.ObfuscationParameterKeyRegex, _settings.ObfuscationParameterValueRegex, _settings.Rules);
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
                    }
                    else
                    {
                        _settings.Enabled = false;
                    }

                    _instrumentationGateway.EndRequest += ReportWafInitInfoOnce;
                    LifetimeManager.Instance.AddShutdownTask(RunShutdown);
                    _rateLimiter = new AppSecRateLimiter(_settings.TraceRateLimit);
                }
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
            get
            {
                return LazyInitializer.EnsureInitialized(ref _instance, ref _globalInstanceInitialized, ref _globalInstanceLock);
            }

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

        /// <summary>
        /// Gets <see cref="SecuritySettings"/> instance
        /// </summary>
        SecuritySettings IDatadogSecurity.Settings => _settings;

        internal SecuritySettings Settings => _settings;

        internal Version DdlibWafVersion => _waf?.Version;

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
            try
            {
                Log.Debug("Sending CallTarget AppSec integration definitions to native library.");
                var payload = InstrumentationDefinitions.GetAllDefinitions(InstrumentationCategory.AppSec);
                NativeMethods.InitializeProfiler(payload.DefinitionsId, payload.Definitions);
                foreach (var def in payload.Definitions)
                {
                    def.Dispose();
                }

                Log.Information<int>("The profiler has been initialized with {count} AppSec definitions.", payload.Definitions.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }

            try
            {
                Log.Debug("Sending CallTarget appsec derived integration definitions to native library.");
                var payload = InstrumentationDefinitions.GetDerivedDefinitions(InstrumentationCategory.AppSec);
                NativeMethods.InitializeProfiler(payload.DefinitionsId, payload.Definitions);
                foreach (var def in payload.Definitions)
                {
                    def.Dispose();
                }

                Log.Information<int>("The profiler has been initialized with {count} AppSec derived definitions.", payload.Definitions.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
        }

        /// <summary>
        /// Frees resources
        /// </summary>
        public void Dispose()
        {
            _waf?.Dispose();
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
            var resultData = result.Data;
            SetTraceSamplingPriority(span);

            LogMatchesIfDebugEnabled(resultData, blocked);

            span.SetTag(Tags.AppSecJson, "{\"triggers\":" + resultData + "}");
            var clientIp = span.GetTag(Tags.HttpClientIp);
            if (!string.IsNullOrEmpty(clientIp))
            {
                span.SetTag(Tags.ActorIp, clientIp);
            }

            span.SetTag(Tags.Origin, "appsec");
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

        private void AddResponseHeaderTags(ITransport transport, Span span)
        {
            TryAddEndPoint(span);
            var headers = transport.GetResponseHeaders();
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

        private void AggregateAddressesInContext(object sender, InstrumentationGatewaySecurityEventArgs e)
        {
            var context = GetOrCreateContext(e.Transport);
            context.AggregateAddresses(e.EventData, e.OverrideExistingAddress);
        }

        // NOTE: This method disposes of the WAF context, so it should be run once,
        // and only once, at the end of the request.
        private void RunWaf(object sender, InstrumentationGatewaySecurityEventArgs e)
        {
            try
            {
                using var additiveContext = GetOrCreateContext(e.Transport);
                additiveContext.AggregateAddresses(e.EventData, e.OverrideExistingAddress);
                var span = GetLocalRootSpan(e.RelatedSpan);

                AnnotateSpan(span);

                // run the WAF and execute the results
                using var wafResult = additiveContext.Run(_settings.WafTimeoutMicroSeconds);
                if (wafResult.ReturnCode == ReturnCode.Monitor || wafResult.ReturnCode == ReturnCode.Block)
                {
                    var block = wafResult.ReturnCode == ReturnCode.Block;
                    if (block)
                    {
                        // blocking has been removed, waiting a better implementation
                    }

                    Report(e.Transport, span, wafResult, block);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Call into the security module failed");
            }
        }

        private void ReportWafInitInfoOnce(object sender, InstrumentationGatewaySecurityEventArgs e)
        {
            _instrumentationGateway.EndRequest -= ReportWafInitInfoOnce;
            var span = e.RelatedSpan.Context.TraceContext.RootSpan ?? e.RelatedSpan;
            span.Context.TraceContext?.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Asm);
            span.SetMetric(Metrics.AppSecWafInitRulesLoaded, _waf.InitializationResult.LoadedRules);
            span.SetMetric(Metrics.AppSecWafInitRulesErrorCount, _waf.InitializationResult.FailedToLoadRules);
            if (_waf.InitializationResult.HasErrors)
            {
                span.SetTag(Tags.AppSecWafInitRuleErrors, _waf.InitializationResult.ErrorMessage);
            }

            span.SetTag(Tags.AppSecWafVersion, _waf.Version.ToString());
        }

        private void RunShutdown()
        {
            if (_instrumentationGateway != null)
            {
                _instrumentationGateway.PathParamsAvailable -= AggregateAddressesInContext;
                _instrumentationGateway.BodyAvailable -= AggregateAddressesInContext;
                _instrumentationGateway.EndRequest -= RunWaf;

#if NETFRAMEWORK
                if (_usingIntegratedPipeline)
                {
                    _instrumentationGateway.LastChanceToWriteTags -= InstrumentationGateway_AddHeadersResponseTags;
                }
#else
                _instrumentationGateway.LastChanceToWriteTags -= InstrumentationGateway_AddHeadersResponseTags;
#endif
            }

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
