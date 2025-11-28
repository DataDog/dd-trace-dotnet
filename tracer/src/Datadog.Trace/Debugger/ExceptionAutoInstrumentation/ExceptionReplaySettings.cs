// <copyright file="ExceptionReplaySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal sealed class ExceptionReplaySettings
    {
        public const int DefaultMaxFramesToCapture = 4;
        public const int DefaultRateLimitSeconds = 60 * 60; // 1 hour
        public const int DefaultMaxExceptionAnalysisLimit = 100;
        private const string DefaultSite = "datadoghq.com";

        public ExceptionReplaySettings(IConfigurationSource? source, IConfigurationTelemetry telemetry)
        {
            source ??= NullConfigurationSource.Instance;
            var config = new ConfigurationBuilder(source, telemetry);

#pragma warning disable CS0612 // Type or member is obsolete
            var erEnabledResult = config.WithKeys(ConfigurationKeys.Debugger.ExceptionReplayEnabled, fallbackKey: ConfigurationKeys.Debugger.ExceptionDebuggingEnabled).AsBoolResult();
#pragma warning restore CS0612 // Type or member is obsolete
            Enabled = erEnabledResult.WithDefault(false);
            CanBeEnabled = erEnabledResult.ConfigurationResult is not { IsValid: true, Result: false };

            CaptureFullCallStack = config.WithKeys(ConfigurationKeys.Debugger.ExceptionReplayCaptureFullCallStackEnabled).AsBool(false);

            var maximumFramesToCapture = config
                                        .WithKeys(ConfigurationKeys.Debugger.ExceptionReplayCaptureMaxFrames)
                                        .AsInt32(DefaultMaxFramesToCapture, maxDepth => maxDepth > 0)
                                        .Value;

            MaximumFramesToCapture = CaptureFullCallStack ? short.MaxValue : maximumFramesToCapture;

            var seconds = config
                         .WithKeys(ConfigurationKeys.Debugger.RateLimitSeconds)
                         .AsInt32(DefaultRateLimitSeconds);

            RateLimit = TimeSpan.FromSeconds(seconds);

            MaxExceptionAnalysisLimit = config
                                       .WithKeys(ConfigurationKeys.Debugger.MaxExceptionAnalysisLimit)
                                       .AsInt32(DefaultMaxExceptionAnalysisLimit, x => x > 0)
                                       .Value;

            AgentlessEnabled = config.WithKeys(ConfigurationKeys.Debugger.ExceptionReplayAgentlessEnabled).AsBool(false);
            AgentlessUrlOverride = config.WithKeys(ConfigurationKeys.Debugger.ExceptionReplayAgentlessUrl).AsString();
            AgentlessApiKey = config.WithKeys(ConfigurationKeys.ApiKey).AsRedactedString();
            AgentlessSite = config.WithKeys(ConfigurationKeys.Site).AsString(DefaultSite, site => !string.IsNullOrEmpty(site)) ?? DefaultSite;
        }

        public bool Enabled { get; }

        public bool CanBeEnabled { get; }

        public int MaximumFramesToCapture { get; }

        public bool CaptureFullCallStack { get; }

        public TimeSpan RateLimit { get; }

        public int MaxExceptionAnalysisLimit { get; }

        public bool AgentlessEnabled { get; }

        public string? AgentlessUrlOverride { get; }

        public string? AgentlessApiKey { get; }

        public string AgentlessSite { get; }

        public static ExceptionReplaySettings FromSource(IConfigurationSource source, IConfigurationTelemetry telemetry)
        {
            return new ExceptionReplaySettings(source, telemetry);
        }

        public static ExceptionReplaySettings FromDefaultSource()
        {
            return FromSource(GlobalConfigurationSource.Instance, TelemetryFactory.Config);
        }
    }
}
