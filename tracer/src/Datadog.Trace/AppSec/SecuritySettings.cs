// <copyright file="SecuritySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.AppSec
{
    internal class SecuritySettings
    {
        public const string UserTrackingExtendedMode = "extended";
        public const string UserTrackingSafeMode = "safe";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SecuritySettings>();

        public SecuritySettings(IConfigurationSource? source, IConfigurationTelemetry telemetry)
        {
            source ??= NullConfigurationSource.Instance;
            var config = new ConfigurationBuilder(source, telemetry);
            BlockedHtmlTemplate = config
                                 .WithKeys(ConfigurationKeys.AppSec.HtmlBlockedTemplate)
                                 .AsRedactedString(SecurityConstants.BlockedHtmlTemplate); // Redacted because it's huge

            BlockedJsonTemplate = config
                                 .WithKeys(ConfigurationKeys.AppSec.JsonBlockedTemplate)
                                 .AsString(SecurityConstants.BlockedJsonTemplate);

            // both should default to false
            var enabledEnvVar = config
                               .WithKeys(ConfigurationKeys.AppSec.Enabled)
                               .AsBool();

            Enabled = enabledEnvVar ?? false;
            CanBeToggled = enabledEnvVar == null;

            Rules = config.WithKeys(ConfigurationKeys.AppSec.Rules).AsString();
            CustomIpHeader = config.WithKeys(ConfigurationKeys.AppSec.CustomIpHeader).AsString();
            var extraHeaders = config.WithKeys(ConfigurationKeys.AppSec.ExtraHeaders).AsString();
            ExtraHeaders = !string.IsNullOrEmpty(extraHeaders) ? extraHeaders!.Split(',') : Array.Empty<string>();
            KeepTraces = config.WithKeys(ConfigurationKeys.AppSec.KeepTraces).AsBool(true);

            // empty or junk values to default to 100, any number is valid, with zero or less meaning limit off
            TraceRateLimit = config.WithKeys(ConfigurationKeys.AppSec.TraceRateLimit).AsInt32(100);

            WafTimeoutMicroSeconds = (ulong)config
                                           .WithKeys(ConfigurationKeys.AppSec.WafTimeout)
                                           .GetAs<int>(
                                                getDefaultValue: () => 100_000, // Default timeout of 100 ms, only extreme conditions should cause timeout
                                                converter: ParseWafTimeout,
                                                validator: wafTimeout => wafTimeout > 0);

            ObfuscationParameterKeyRegex = config
                                          .WithKeys(ConfigurationKeys.AppSec.ObfuscationParameterKeyRegex)
                                          .AsString(SecurityConstants.ObfuscationParameterKeyRegexDefault, x => !string.IsNullOrWhiteSpace(x));

            ObfuscationParameterValueRegex = config
                                            .WithKeys(ConfigurationKeys.AppSec.ObfuscationParameterValueRegex)
                                            .AsString(SecurityConstants.ObfuscationParameterValueRegexDefault, x => !string.IsNullOrWhiteSpace(x));

            UserEventsAutomatedTracking = config
                                         .WithKeys(ConfigurationKeys.AppSec.UserEventsAutomatedTracking)
                                         .AsString(
                                              UserTrackingSafeMode,
                                              val =>
                                                  val.Equals("disabled", StringComparison.OrdinalIgnoreCase)
                                               || val.Equals(UserTrackingSafeMode, StringComparison.OrdinalIgnoreCase)
                                               || val.Equals(UserTrackingExtendedMode, StringComparison.OrdinalIgnoreCase))
                                         .ToLowerInvariant();

            ApiSecuritySampling = config
                                 .WithKeys(ConfigurationKeys.AppSec.ApiSecurityRequestSampleRate)
                                 .AsDouble(val => val is <= 1 and >= 0)
                                 .GetValueOrDefault(0.1);

            ApiSecurityEnabled = config.WithKeys(ConfigurationKeys.AppSec.ApiExperimentalSecurityEnabled)
                                       .AsBool(false);
        }

        public double ApiSecuritySampling { get; }

        public bool Enabled { get; }

        public bool CanBeToggled { get; }

        public string? CustomIpHeader { get; }

        /// <summary>
        /// Gets keys indicating the optional custom appsec headers the user wants to send.
        /// </summary>
        public IReadOnlyList<string> ExtraHeaders { get; }

        /// <summary>
        /// Gets the path to a user-defined WAF Rules file.
        /// Default is null, meaning uses embedded rule set
        /// </summary>
        public string? Rules { get; }

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

        /// <summary>
        /// Gets the blocking response template for Html content. This template is used in combination with the status code to craft and send a response upon blocking the request.
        /// </summary>
        public string BlockedHtmlTemplate { get; }

        /// <summary>
        /// Gets the automatic tracking of user events mode. Values can be disabled, safe or extended.
        /// </summary>
        public string UserEventsAutomatedTracking { get; }

        /// <summary>
        /// Gets the response template for Json content. This template is used in combination with the status code to craft and send a response upon blocking the request.
        /// </summary>
        public string BlockedJsonTemplate { get; }

        /// <summary>
        /// Gets a value indicating whether or not api security is enabled, defaults to false.
        /// </summary>
        public bool ApiSecurityEnabled { get; }

        public static SecuritySettings FromDefaultSources()
        {
            return new SecuritySettings(GlobalConfigurationSource.Instance, TelemetryFactory.Config);
        }

        private static ParsingResult<int> ParseWafTimeout(string wafTimeoutString)
        {
            if (string.IsNullOrWhiteSpace(wafTimeoutString))
            {
                Log.Warning("Ignoring '{WafTimeoutKey}' of '{WafTimeoutString}' because it was zero or less", ConfigurationKeys.AppSec.WafTimeout, wafTimeoutString);
                return ParsingResult<int>.Failure();
            }

            var numberStyles = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.Any;
            if (int.TryParse(wafTimeoutString, numberStyles, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            wafTimeoutString = wafTimeoutString.Trim();

            int multipler = 1;
            string? intPart = null;

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
                return ParsingResult<int>.Failure();
            }

            if (int.TryParse(intPart, numberStyles, CultureInfo.InvariantCulture, out result))
            {
                return result * multipler;
            }

            return ParsingResult<int>.Failure();
        }
    }
}
