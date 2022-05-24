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
        internal const string ObfuscationParameterKeyRegexDefault = @"(?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?)key)|token|consumer_?(?:id|key|secret)|sign(?:ed|ature)|bearer|authorization";
        internal const string ObfuscationParameterValueRegexDefault = @"(?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?|access_?|secret_?)key(?:_?id)?|token|consumer_?(?:id|key|secret)|sign(?:ed|ature)?|auth(?:entication|orization)?)(?:\s*=[^;]|""\s*:\s*""[^""]+"")|bearer\s+[a-z0-9\._\-]+|token:[a-z0-9]{13}|gh[opsu]_[0-9a-zA-Z]{36}|ey[I-L][\w=-]+\.ey[I-L][\w=-]+(?:\.[\w.+\/=-]+)?|[\-]{5}BEGIN[a-z\s]+PRIVATE\sKEY[\-]{5}[^\-]+[\-]{5}END[a-z\s]+PRIVATE\sKEY|ssh-rsa\s*[a-z0-9\/\.+]{100,}";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SecuritySettings>();

        public SecuritySettings(IConfigurationSource source)
        {
            // both should default to false
            Enabled = source?.GetBool(ConfigurationKeys.AppSec.Enabled) ?? false;
            Rules = source?.GetString(ConfigurationKeys.AppSec.Rules);
            CustomIpHeader = source?.GetString(ConfigurationKeys.AppSec.CustomIpHeader);
            var extraHeaders = source?.GetString(ConfigurationKeys.AppSec.ExtraHeaders);
            ExtraHeaders = !string.IsNullOrEmpty(extraHeaders) ? extraHeaders.Split(',') : Array.Empty<string>();
            KeepTraces = source?.GetBool(ConfigurationKeys.AppSec.KeepTraces) ?? true;

            // empty or junk values to default to 100, any number is valid, with zero or less meaning limit off
            TraceRateLimit = source?.GetInt32(ConfigurationKeys.AppSec.TraceRateLimit) ?? 100;

            var wafTimeoutString = source?.GetString(ConfigurationKeys.AppSec.WafTimeout);
            const int defaultWafTimeout = 100_000;
            if (string.IsNullOrWhiteSpace(wafTimeoutString))
            {
                WafTimeoutMicroSeconds = defaultWafTimeout;
            }
            else
            {
                // Default timeout of 100 ms, only extreme conditions should cause timeout
                var wafTimeout = ParseWafTimeout(wafTimeoutString);
                if (wafTimeout <= 0)
                {
                    Log.Warning<string, string>("Ignoring '{WafTimeoutKey}' of '{wafTimeoutString}' because it was zero or less", ConfigurationKeys.AppSec.WafTimeout, wafTimeoutString);
                    wafTimeout = defaultWafTimeout;
                }

                WafTimeoutMicroSeconds = (ulong)wafTimeout;
            }

            var obfuscationParameterKeyRegex = source?.GetString(ConfigurationKeys.AppSec.ObfuscationParameterKeyRegex);
            ObfuscationParameterKeyRegex = string.IsNullOrWhiteSpace(obfuscationParameterKeyRegex) ? ObfuscationParameterKeyRegexDefault : obfuscationParameterKeyRegex;

            var obfuscationParameterValueRegex = source?.GetString(ConfigurationKeys.AppSec.ObfuscationParameterValueRegex);
            ObfuscationParameterValueRegex = string.IsNullOrWhiteSpace(obfuscationParameterValueRegex) ? ObfuscationParameterValueRegexDefault : obfuscationParameterValueRegex;
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

        /// <summary>
        /// Gets the regex that will be used to obfuscate possible sensitive data in keys that are highlighted WAF as potentially malicious
        /// </summary>
        public string ObfuscationParameterKeyRegex { get; }

        /// <summary>
        /// Gets the regex that will be used to obfuscate possible sensitive data in values that are highlighted WAF as potentially malicious
        /// </summary>
        public string ObfuscationParameterValueRegex { get; }

        public static SecuritySettings FromDefaultSources()
        {
            var source = GlobalSettings.CreateDefaultConfigurationSource();
            return new SecuritySettings(source);
        }

        private static int ParseWafTimeout(string wafTimeoutString)
        {
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
