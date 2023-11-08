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
            /// Automatic tracking of user events mode. Values can be disabled, safe or extended.
            /// </summary>
            internal const string UserEventsAutomatedTracking = "DD_APPSEC_AUTOMATED_USER_EVENTS_TRACKING";

            /// <summary>
            /// Percentage of requests for which the schema should be extracted. Between 0 and 1, defaults to 0.1 (10%)
            /// A value of 0 means no schemas are extracted, effectively disabling schema extraction altogether
            /// </summary>
            internal const string ApiSecurityRequestSampleRate = "DD_API_SECURITY_REQUEST_SAMPLE_RATE";

            /// <summary>
            /// Unless set to true or 1, tracers don’t collect schemas. After the experiment, the environment variable will be removed and schema collection will be enabled only when ASM is enabled
            /// </summary>
            internal const string ApiExperimentalSecurityEnabled = "DD_EXPERIMENTAL_API_SECURITY_ENABLED";
        }
    }
}
