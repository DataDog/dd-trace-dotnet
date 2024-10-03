// <copyright file="ConfigurationKeys.AppSec.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Configuration
{
    internal partial class ConfigurationKeys
    {
        internal class AppSec
        {
            /// <summary>
            /// Configuration key for enabling or disabling the AppSec.
            /// Default is value is false (disabled).
            /// </summary>
            public const string Enabled = "DD_APPSEC_ENABLED";

            /// <summary>
            /// Override the default rules file provided. Must be a path to a valid JSON rules file.
            /// Default is value is null (do not override).
            /// </summary>
            public const string Rules = "DD_APPSEC_RULES";

            /// <summary>
            /// Configuration key indicating the optional name of the custom header to take into account for the ip address.
            /// Default is value is null (do not override).
            /// </summary>
            public const string CustomIpHeader = "DD_APPSEC_IPHEADER";

            /// <summary>
            /// Comma separated keys indicating the optional custom headers the user wants to send.
            /// Default is value is null.
            /// </summary>
            public const string ExtraHeaders = "DD_APPSEC_EXTRA_HEADERS";

            /// <summary>
            /// Specifies if the AppSec traces should be explicitly kept or dropped.
            /// Default is true, to keep all traces, false means drop all traces (by setting AutoReject as sampling priority).
            /// For internal testing only.
            /// </summary>
            internal const string KeepTraces = "DD_APPSEC_KEEP_TRACES";

            /// <summary>
            /// Limits the amount of AppSec traces sent per second with an integer value, strictly positive.
            /// </summary>
            internal const string TraceRateLimit = "DD_APPSEC_TRACE_RATE_LIMIT";

            /// <summary>
            /// WAF timeout in microseconds of each WAF execution (the timeout value passed to ddwaf_run).
            /// </summary>
            internal const string WafTimeout = "DD_APPSEC_WAF_TIMEOUT";

            /// <summary>
            /// default value to true. Set to false to disable exploit prevention.
            /// </summary>
            internal const string RaspEnabled = "DD_APPSEC_RASP_ENABLED";

            /// <summary>
            /// with a default value of true, it allows a customer to disable the generation of stack traces, for any ASM-specific purpose such as RASP.
            /// </summary>
            internal const string StackTraceEnabled = "DD_APPSEC_STACK_TRACE_ENABLED";

            /// <summary>
            /// with a default value of 2, defines the maximum number of stack traces to be reported due to RASP events. 0 for unlimited.
            /// </summary>
            internal const string MaxStackTraces = "DD_APPSEC_MAX_STACK_TRACES";

            /// <summary>
            /// with a default value of 32, defines the maximum depth of a stack trace to be reported due to RASP events. O for unlimited.
            /// </summary>
            internal const string MaxStackTraceDepth = "DD_APPSEC_MAX_STACK_TRACE_DEPTH";

            /// <summary>
            /// with a default value of 75, defines the percentage of frames taken from the top of the stack when trimming. Min 0, Max 100
            /// </summary>
            internal const string MaxStackTraceDepthTopPercent = "DD_APPSEC_MAX_STACK_TRACE_DEPTH_TOP_PERCENT";

            /// <summary>
            /// The regex that will be used to obfuscate possible sensitive data in keys that are highlighted WAF as potentially malicious
            /// </summary>
            internal const string ObfuscationParameterKeyRegex = "DD_APPSEC_OBFUSCATION_PARAMETER_KEY_REGEXP";

            /// <summary>
            /// The regex that will be used to obfuscate possible sensitive data in values that are highlighted WAF as potentially malicious
            /// </summary>
            internal const string ObfuscationParameterValueRegex = "DD_APPSEC_OBFUSCATION_PARAMETER_VALUE_REGEXP";

            /// <summary>
            /// Blocking response template for HTML content. This template is used in combination with the status code to craft and send a response upon blocking the request.
            /// </summary>
            internal const string HtmlBlockedTemplate = "DD_APPSEC_HTTP_BLOCKED_TEMPLATE_HTML";

            /// <summary>
            /// Blocking response template for Json content. This template is used in combination with the status code to craft and send a response upon blocking the request.
            /// </summary>
            internal const string JsonBlockedTemplate = "DD_APPSEC_HTTP_BLOCKED_TEMPLATE_JSON";

            /// <summary>
            /// Deprecate. Automatic tracking of user events mode. Values can be disabled, safe or extended.
            /// This config is in the process of being deprecated. Please use DD_APPSEC_AUTO_USER_INSTRUMENTATION_MODE
            /// instead.
            /// Values will be automatically translated:
            /// disabled = disabled
            /// safe = anon
            /// extended = ident
            /// </summary>
            internal const string UserEventsAutomatedTracking = "DD_APPSEC_AUTOMATED_USER_EVENTS_TRACKING";

            /// <summary>
            /// Automatic instrumentation of user event mode. Values can be ident, disabled, anon.
            /// </summary>
            internal const string UserEventsAutoInstrumentationMode = "DD_APPSEC_AUTO_USER_INSTRUMENTATION_MODE";

            /// <summary>
            /// When ASM is enabled, collects in spans endpoints apis schemas analyzed by the waf, default value is true.
            /// </summary>
            internal const string ApiSecurityEnabled = "DD_API_SECURITY_ENABLED";

            /// <summary>
            /// Api security sample delay in seconds , should be a float. Set to 0 for testing purposes. default value of 30.
            /// </summary>
            internal const string ApiSecuritySampleDelay = "DD_API_SECURITY_SAMPLE_DELAY";

            /// <summary>
            /// Use new unsafe encoder for the waf
            /// </summary>
            internal const string UseUnsafeEncoder = "DD_EXPERIMENTAL_APPSEC_USE_UNSAFE_ENCODER";

            /// <summary>
            /// Activate debug logs for the WAF
            /// </summary>
            internal const string WafDebugEnabled = "DD_APPSEC_WAF_DEBUG";

            /// <summary>
            /// Activate SCA (Software Composition Analysis), used in the backend
            /// </summary>
            internal const string ScaEnabled = "DD_APPSEC_SCA_ENABLED";
        }
    }
}
