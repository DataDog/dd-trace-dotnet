// <copyright file="ExceptionReplaySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
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

#pragma warning disable CS0612 // Type or member is obsolete
            Enabled = config.WithKeys(ConfigurationKeys.Debugger.ExceptionDebuggingEnabled, ConfigurationKeys.Debugger.ExceptionReplayEnabled).AsBool(false);
#pragma warning restore CS0612 // Type or member is obsolete

            CaptureFullCallStack = config.WithKeys(ConfigurationKeys.Debugger.ExceptionReplayCaptureFullCallStackEnabled).AsBool(false);

            var maximumFramesToCapture = config
                                        .WithKeys(ConfigurationKeys.Debugger.ExceptionReplayMaxFramesToCapture)
                                        .AsInt32(DefaultMaxFramesToCapture, maxDepth => maxDepth > 0)
                                        .Value;

            MaximumFramesToCapture = CaptureFullCallStack ? short.MaxValue : maximumFramesToCapture;

            var seconds = config
                         .WithKeys(ConfigurationKeys.Debugger.RateLimitSeconds)
                         .AsInt32(DefaultRateLimitSeconds, x => x > 0)
                         .Value;

            RateLimit = TimeSpan.FromSeconds(seconds);

            MaxExceptionAnalysisLimit = config
                                       .WithKeys(ConfigurationKeys.Debugger.MaxExceptionAnalysisLimit)
                                       .AsInt32(DefaultMaxExceptionAnalysisLimit, x => x > 0)
                                       .Value;
        }

        public bool Enabled { get; }

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
