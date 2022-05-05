// <copyright file="ConfigurationKeys.AppSec.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            /// Limits the amount of AppSec traces sent per second with an integer value, strictly positive.
            /// </summary>
            internal const string TraceRateLimit = "DD_APPSEC_TRACE_RATE_LIMIT";

            /// <summary>
            /// WAF timeout in microseconds of each WAF execution (the timeout value passed to ddwaf_run).
            /// </summary>
            internal const string WafTimeout = "DD_APPSEC_WAF_TIMEOUT";

            /// <summary>
            /// The regex that will be used to obfuscate possible senative data in keys that are highlighted WAF as potentially malicious
            /// </summary>
            internal const string ObfuscationParameterKeyRegex = "DD_APPSEC_OBFUSCATION_PARAMETER_KEY_REGEXP";

            /// <summary>
            /// The regex that will be used to obfuscate possible senative data in values that are highlighted WAF as potentially malicious
            /// </summary>
            internal const string ObfuscationParameterValueRegex = "DD_APPSEC_OBFUSCATION_PARAMETER_VALUE_REGEXP";
        }
    }
}
