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
        public const string UserTrackingDisabled = "disabled";
        public const string UserTrackingIdentMode = "identification";
        public const string UserTrackingIdentShortMode = "ident";
        public const string UserTrackingAnonMode = "anonymization";
        public const string UserTrackingAnonShortMode = "anon";
        private const string DeprecatedUserTrackingExtendedMode = "extended";
        private const string DeprecatedUserTrackingSafeMode = "safe";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SecuritySettings>();

        public SecuritySettings(IConfigurationSource? source, IConfigurationTelemetry telemetry)
        {
            source ??= NullConfigurationSource.Instance;
            var config = new ConfigurationBuilder(source, telemetry);
            BlockedHtmlTemplatePath = config
                                 .WithKeys(ConfigurationKeys.AppSec.HtmlBlockedTemplate)
                                 .AsRedactedString(); // Redacted because it's huge

            BlockedJsonTemplatePath = config
                                 .WithKeys(ConfigurationKeys.AppSec.JsonBlockedTemplate)
                                 .AsString();

            // both should default to false
            var enabledEnvVar = config
                               .WithKeys(ConfigurationKeys.AppSec.Enabled)
                               .AsBoolResult();

            CanBeToggled = !enabledEnvVar.ConfigurationResult.IsValid;
            Enabled = enabledEnvVar.WithDefault(false);

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

            var newConfig =
                config
                   .WithKeys(ConfigurationKeys.AppSec.UserEventsAutoInstrumentationMode)
                   .AsStringResult(
                        val =>
                            val.Equals(UserTrackingDisabled, StringComparison.OrdinalIgnoreCase)
                         || val.Equals(UserTrackingIdentMode, StringComparison.OrdinalIgnoreCase)
                         || val.Equals(UserTrackingIdentShortMode, StringComparison.OrdinalIgnoreCase)
                         || val.Equals(UserTrackingAnonMode, StringComparison.OrdinalIgnoreCase)
                         || val.Equals(UserTrackingAnonShortMode, StringComparison.OrdinalIgnoreCase),
                        ParsingResult<string>.Success);

            if (newConfig.ConfigurationResult.IsPresent)
            {
                UserEventsAutoInstrumentationMode = newConfig.ConfigurationResult.IsValid ? newConfig.ConfigurationResult.Result : UserTrackingDisabled;
            }
            else
            {
                var oldConfig =
                    config
                       .WithKeys(ConfigurationKeys.AppSec.UserEventsAutomatedTracking)
                       .AsStringResult(
                            val =>
                                val.Equals(UserTrackingDisabled, StringComparison.OrdinalIgnoreCase)
                             || val.Equals(DeprecatedUserTrackingSafeMode, StringComparison.OrdinalIgnoreCase)
                             || val.Equals(DeprecatedUserTrackingExtendedMode, StringComparison.OrdinalIgnoreCase),
                            ParsingResult<string>.Success);

                if (oldConfig.ConfigurationResult.IsPresent)
                {
                    UserEventsAutoInstrumentationMode = oldConfig.ConfigurationResult.IsValid ? oldConfig.ConfigurationResult.Result : UserTrackingDisabled;
                }
                else
                {
                    // ident mode is default with nothing present
                    UserEventsAutoInstrumentationMode = UserTrackingIdentMode;
                }
            }

            if (UserEventsAutoInstrumentationMode == DeprecatedUserTrackingSafeMode
                || UserEventsAutoInstrumentationMode == UserTrackingAnonShortMode)
            {
                UserEventsAutoInstrumentationMode = UserTrackingAnonMode;
            }

            if (UserEventsAutoInstrumentationMode == DeprecatedUserTrackingExtendedMode
                || UserEventsAutoInstrumentationMode == UserTrackingIdentShortMode)
            {
                UserEventsAutoInstrumentationMode = UserTrackingIdentMode;
            }

            ApiSecurityEnabled = config.WithKeys(ConfigurationKeys.AppSec.ApiSecurityEnabled, "DD_EXPERIMENTAL_API_SECURITY_ENABLED")
                                       .AsBool(true);

            ApiSecuritySampleDelay = config.WithKeys(ConfigurationKeys.AppSec.ApiSecuritySampleDelay)
                                           .AsDouble(30.0, val => val >= 0.0)
                                           .Value;

            UseUnsafeEncoder = config.WithKeys(ConfigurationKeys.AppSec.UseUnsafeEncoder)
                                     .AsBool(false);

            // For now, RASP is enabled by default.
            RaspEnabled = config.WithKeys(ConfigurationKeys.AppSec.RaspEnabled)
                                .AsBool(true) && Enabled;

            StackTraceEnabled = config.WithKeys(ConfigurationKeys.AppSec.StackTraceEnabled)
                                      .AsBool(true);

            MaxStackTraces = config
                                  .WithKeys(ConfigurationKeys.AppSec.MaxStackTraces)
                                  .AsInt32(defaultValue: 2, validator: val => val >= 1)
                                  .Value;

            MaxStackTraceDepth = config
                                  .WithKeys(ConfigurationKeys.AppSec.MaxStackTraceDepth)
                                  .AsInt32(defaultValue: 32, validator: val => val >= 1)
                                  .Value;

            MaxStackTraceDepthTopPercent = config
                                  .WithKeys(ConfigurationKeys.AppSec.MaxStackTraceDepthTopPercent)
                                  .AsInt32(defaultValue: 75, validator: val => val >= 0 && val <= 100)
                                  .Value;

            WafDebugEnabled = config
                             .WithKeys(ConfigurationKeys.AppSec.WafDebugEnabled)
                             .AsBool(defaultValue: false);

            ScaEnabled = config
                             .WithKeys(ConfigurationKeys.AppSec.ScaEnabled)
                             .AsBool();
        }

        public double ApiSecuritySampleDelay { get; set; }

        public double ApiSecuritySampling { get; }

        public int ApiSecurityMaxConcurrentRequests { get; }

        public bool Enabled { get; }

        public bool UseUnsafeEncoder { get; }

        public bool WafDebugEnabled { get; }

        public bool CanBeToggled { get; }

        public string? CustomIpHeader { get; }

        // RASP related variables

        public bool RaspEnabled { get; }

        public bool StackTraceEnabled { get; }

        public int MaxStackTraces { get; }

        public int MaxStackTraceDepth { get; }

        public int MaxStackTraceDepthTopPercent { get; }

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
        public string? BlockedHtmlTemplatePath { get; }

        /// <summary>
        /// Gets the Automatic instrumentation of user event mode. Values can be ident, disabled, anon.
        /// </summary>
        public string UserEventsAutoInstrumentationMode { get; }

        /// <summary>
        /// Gets the response template for Json content. This template is used in combination with the status code to craft and send a response upon blocking the request.
        /// </summary>
        public string? BlockedJsonTemplatePath { get; }

        /// <summary>
        /// Gets a value indicating whether or not api security is enabled, defaults to false.
        /// </summary>
        public bool ApiSecurityEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether or not SCA (Software Composition Analysis) is enabled, defaults to null.
        /// It is not use locally, but ready by the backend.
        /// </summary>
        public bool? ScaEnabled { get; }

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
