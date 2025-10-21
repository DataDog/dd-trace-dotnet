// <copyright file="ExceptionReplaySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Registry.Generated;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionReplaySettings
    {
        public const int DefaultMaxFramesToCapture = 4;
        public const int DefaultRateLimitSeconds = 60 * 60; // 1 hour
        public const int DefaultMaxExceptionAnalysisLimit = 100;

        public ExceptionReplaySettings(IConfigurationSource? source, IConfigurationTelemetry telemetry)
        {
            source ??= NullConfigurationSource.Instance;
            var config = new ConfigurationBuilder(source, telemetry);

            var erEnabledResult = config.WithKeys(new ConfigKeyDdExceptionReplayEnabled()).AsBoolResult();
            Enabled = erEnabledResult.WithDefault(false);
            CanBeEnabled = erEnabledResult.ConfigurationResult is not { IsValid: true, Result: false };

            CaptureFullCallStack = config.WithKeys(new ConfigKeyDdExceptionReplayCaptureFullCallstackEnabled()).AsBool(false);

            var maximumFramesToCapture = config
                                        .WithKeys(new ConfigKeyDdExceptionReplayCaptureMaxFrames())
                                        .AsInt32(DefaultMaxFramesToCapture, maxDepth => maxDepth > 0)
                                        .Value;

            MaximumFramesToCapture = CaptureFullCallStack ? short.MaxValue : maximumFramesToCapture;

            var seconds = config
                         .WithKeys(new ConfigKeyDdExceptionReplayRateLimitSeconds())
                         .AsInt32(DefaultRateLimitSeconds);

            RateLimit = TimeSpan.FromSeconds(seconds);

            MaxExceptionAnalysisLimit = config
                                       .WithKeys(new ConfigKeyDdExceptionReplayMaxExceptionAnalysisLimit())
                                       .AsInt32(DefaultMaxExceptionAnalysisLimit, x => x > 0)
                                       .Value;
        }

        public bool Enabled { get; }

        public bool CanBeEnabled { get; }

        public int MaximumFramesToCapture { get; }

        public bool CaptureFullCallStack { get; }

        public TimeSpan RateLimit { get; }

        public int MaxExceptionAnalysisLimit { get; }

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
