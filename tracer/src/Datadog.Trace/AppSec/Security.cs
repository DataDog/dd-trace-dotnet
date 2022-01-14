// <copyright file="Security.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.AppSec.Transports;
using Datadog.Trace.AppSec.Transports.Http;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec
{
    /// <summary>
    /// The Secure is responsible coordinating app sec
    /// </summary>
    internal class Security : IDatadogSecurity, IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Security>();

        private static readonly Dictionary<string, string> RequestHeaders;
        private static readonly Dictionary<string, string> ResponseHeaders;

        private static Security _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();

        private readonly IWaf _waf;
        private readonly InstrumentationGateway _instrumentationGateway;
        private readonly SecuritySettings _settings;

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

                _settings.Enabled = _settings.Enabled && AreArchitectureAndOsSupported();
                if (_settings.Enabled)
                {
                    _waf = waf ?? Waf.Waf.Create(_settings.Rules);
                    if (_waf != null)
                    {
                        _instrumentationGateway.RequestEnd += InstrumentationGatewayInstrumentationGatewayEvent;
#if NETFRAMEWORK
                        if (System.Web.HttpRuntime.UsingIntegratedPipeline)
                        {
                            _instrumentationGateway.LastChanceToWriteTags += InstrumentationGateway_AddHeadersResponseTags;
                        }
#else
                        _instrumentationGateway.LastChanceToWriteTags += InstrumentationGateway_AddHeadersResponseTags;
#endif
                    }
                    else
                    {
                        _settings.Enabled = false;
                    }

                    LifetimeManager.Instance.AddShutdownTask(RunShutdown);
                }
            }
            catch (Exception ex)
            {
                _settings = new(source: null) { Enabled = false };
                Log.Error(ex, "AppSec could not start because of an unexpected error. No security activities will be collected. Please contact support at https://docs.datadoghq.com/help/ for help.");
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
                    Log.Debug(blocked ? "Blocking current transaction (rule: {RuleId})" : "Detecting an attack from rule {RuleId}", match.Rule);
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

        private static bool AreArchitectureAndOsSupported()
        {
            var frameworkDescription = FrameworkDescription.Instance;
            var osSupported = false;
            var supportedOs = new[]
            {
                OSPlatform.Linux,
                OSPlatform.MacOS,
                OSPlatform.Windows
            };
            if (supportedOs.Contains(frameworkDescription.OSPlatform))
            {
                osSupported = true;
            }

            var archSupported = false;
            var supportedArchs = new[]
            {
                ProcessArchitecture.Arm,
                ProcessArchitecture.X64,
                ProcessArchitecture.X86
            };
            if (supportedArchs.Contains(frameworkDescription.ProcessArchitecture))
            {
                archSupported = true;
            }

            if (!osSupported || !archSupported)
            {
                Log.Error(
                    "AppSec could not start because the current environment is not supported. No security activities will be collected. Please contact support at https://docs.datadoghq.com/help/ for help. Host information: operating_system: {{ {OSPlatform} }}, arch: {{ {ProcessArchitecture} }}, runtime_infos: {{ {ProductVersion} }}",
                    frameworkDescription.OSPlatform,
                    frameworkDescription.ProcessArchitecture,
                    frameworkDescription.ProductVersion);
            }

            return osSupported && archSupported;
        }

        /// <summary>
        /// Frees resources
        /// </summary>
        public void Dispose() => _waf?.Dispose();

        private void InstrumentationGateway_AddHeadersResponseTags(object sender, InstrumentationGatewayEventArgs e)
        {
            if (e.RelatedSpan.GetTag(Tags.AppSecEvent) == "true")
            {
                AddResponseHeaderTags(e.Transport, e.RelatedSpan);
            }
        }

        private void Report(ITransport transport, Span span, string resultData, bool blocked)
        {
            span.SetTag(Tags.AppSecEvent, "true");
            var samplingPirority = _settings.KeepTraces
                                       ? SamplingPriority.UserKeep : SamplingPriority.AutoReject;
            span.SetTraceSamplingPriority(samplingPirority);

            LogMatchesIfDebugEnabled(resultData, blocked);

            span.SetTag(Tags.AppSecJson, "{\"triggers\":" + resultData + "}");

            span.SetTag(Tags.Origin, "appsec");

            var reportedIpInfo = transport.GetReportedIpInfo();
            span.SetTag(Tags.NetworkClientIp, reportedIpInfo.IpAddress);

            var ipInfo = RequestHeadersHelper.ExtractIpAndPort(transport.GetHeader, _settings.CustomIpHeader, _settings.ExtraHeaders, transport.IsSecureConnection, reportedIpInfo);
            span.SetTag(Tags.ActorIp, ipInfo.IpAddress);

            var headers = transport.GetRequestHeaders();
            AddHeaderTags(span, headers, RequestHeaders, SpanContextPropagator.HttpRequestHeadersTagPrefix);
        }

        private void AddResponseHeaderTags(ITransport transport, Span span)
        {
            TryAddEndPoint(span);
            var headers = transport.GetResponseHeaders();
            AddHeaderTags(span, headers, ResponseHeaders, SpanContextPropagator.HttpResponseHeadersTagPrefix);
        }

        private void RunWafAndReact(IDictionary<string, object> args, ITransport transport, Span span)
        {
            span = GetLocalRootSpan(span);

            AnnotateSpan(span);

            var additiveContext = transport.GetAdditiveContext();

            if (additiveContext == null)
            {
                additiveContext = _waf.CreateContext();
                transport.SetAdditiveContext(additiveContext);
            }

            // run the WAF and execute the results
            using var wafResult = additiveContext.Run(args);
            if (wafResult.ReturnCode == ReturnCode.Monitor || wafResult.ReturnCode == ReturnCode.Block)
            {
                var block = _settings.BlockingEnabled && wafResult.ReturnCode == ReturnCode.Block;
                if (block)
                {
                    // blocking has been removed, waiting a better implementation
                }

                Report(transport, span, wafResult.Data, block);
            }
        }

        private void InstrumentationGatewayInstrumentationGatewayEvent(object sender, InstrumentationGatewaySecurityEventArgs e)
        {
            try
            {
                RunWafAndReact(e.EventData, e.Transport, e.RelatedSpan);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Call into the security module failed");
            }
        }

        private void RunShutdown()
        {
            if (_instrumentationGateway != null)
            {
                _instrumentationGateway.RequestEnd -= InstrumentationGatewayInstrumentationGatewayEvent;
#if NETFRAMEWORK
                if (System.Web.HttpRuntime.UsingIntegratedPipeline)
                {
                    _instrumentationGateway.LastChanceToWriteTags -= InstrumentationGateway_AddHeadersResponseTags;
                }
#else
                _instrumentationGateway.LastChanceToWriteTags -= InstrumentationGateway_AddHeadersResponseTags;
#endif
            }

            Dispose();
        }
    }
}
