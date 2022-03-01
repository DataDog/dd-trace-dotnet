// <copyright file="SecuritySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec
{
    internal class SecuritySettings
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SecuritySettings>();

        public SecuritySettings(IConfigurationSource source)
        {
            // both should default to false
            Enabled = source?.GetBool(ConfigurationKeys.AppSecEnabled) ?? false;
            Rules = source?.GetString(ConfigurationKeys.AppSecRules);
            CustomIpHeader = source?.GetString(ConfigurationKeys.AppSecCustomIpHeader);
            var extraHeaders = source?.GetString(ConfigurationKeys.AppSecExtraHeaders);
            ExtraHeaders = !string.IsNullOrEmpty(extraHeaders) ? extraHeaders.Split(',') : Array.Empty<string>();
            KeepTraces = source?.GetBool(ConfigurationKeys.AppSecKeepTraces) ?? true;

            // empty or junk values to default to 100, any number is valid, with zero or less meaning limit off
            TraceRateLimit = source?.GetInt32(ConfigurationKeys.AppSecTraceRateLimit) ?? 100;

            // Default timeout of 100 ms, only extreme conditions should cause timeout
            const int defaultWafTimeout = 100_000;
            var wafTimeoutString = source?.GetString(ConfigurationKeys.AppSecWafTimeout);
            var wafTimeout = ParseWafTimeout(wafTimeoutString);
            if (wafTimeout <= 0)
            {
                wafTimeout = defaultWafTimeout;
                Log.Warning<string, int>("Ignoring '{WafTimeoutKey}'  of '{WafTimeout}' because it was zero or less", ConfigurationKeys.AppSecWafTimeout, wafTimeout);
            }

            WafTimeoutMicroSeconds = (ulong)wafTimeout;
        }

        public bool Enabled { get; set; }

        public string CustomIpHeader { get; }

        /// <summary>
        /// Gets keys indicating the optional custom appsec headers the user wants to send.
        /// </summary>
        public IReadOnlyList<string> ExtraHeaders { get; }

        /// <summary>
        /// Gets the path to a user-defined WAF Rules file.
        /// Default is null, meaning uses embedded rule set
        /// </summary>
        public string Rules { get; }

        /// <summary>
        /// Gets a value indicating whether traces should be mark traces with manual keep below trace rate limit
        /// Default is true
        /// </summary>
        public bool KeepTraces { get; }

        /// <summary>
        /// Gets the limit of AppSec traces sent per second with an integer value, strictly positive.
        /// </summary>
        public int TraceRateLimit { get; }

        /// <summary>
        /// Gets the limit for the amount of time the WAF will perform analysis
        /// </summary>
        public ulong WafTimeoutMicroSeconds { get; }

        public static SecuritySettings FromDefaultSources()
        {
            var source = GlobalSettings.CreateDefaultConfigurationSource();
            return new SecuritySettings(source);
        }

        private static int ParseWafTimeout(string wafTimeoutString)
        {
            if (wafTimeoutString == null)
            {
                return -1;
            }

            var numberStyles = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.Any;
            if (int.TryParse(wafTimeoutString, numberStyles, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            wafTimeoutString = wafTimeoutString.Trim();

            int multipler = 1;
            string intPart = null;

            if (wafTimeoutString.EndsWith("ms"))
            {
                multipler = 1_000;
                intPart = wafTimeoutString.Substring(0, wafTimeoutString.Length - 2);
            }
            else if (wafTimeoutString.EndsWith("us"))
            {
                multipler = 1;
                intPart = wafTimeoutString.Substring(0, wafTimeoutString.Length - 2);
            }
            else if (wafTimeoutString.EndsWith("s"))
            {
                multipler = 1_000_000;
                intPart = wafTimeoutString.Substring(0, wafTimeoutString.Length - 1);
            }

            if (intPart == null)
            {
                return -1;
            }

            if (int.TryParse(intPart, numberStyles, CultureInfo.InvariantCulture, out result))
            {
                return result * multipler;
            }

            return -1;
        }
    }
}
