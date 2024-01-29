// <copyright file="ExceptionDebuggingSettings.cs" company="Datadog">
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
    internal class ExceptionDebuggingSettings
    {
        public const int DefaultMaxFramesToCapture = 3;

        public ExceptionDebuggingSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry)
        {
            source ??= NullConfigurationSource.Instance;
            var config = new ConfigurationBuilder(source, telemetry);

            Enabled = config.WithKeys(ConfigurationKeys.Debugger.ExceptionDebuggingEnabled).AsBool(false);

            CaptureFullCallStack = config.WithKeys(ConfigurationKeys.Debugger.ExceptionDebuggingCaptureFullCallStack).AsBool(false);

            var maximumFramesToCapture = config
                                        .WithKeys(ConfigurationKeys.Debugger.ExceptionDebuggingMaxFramesToCapture)
                                        .AsInt32(DefaultMaxFramesToCapture, maxDepth => maxDepth > 0)
                                        .Value;

            MaximumFramesToCapture = CaptureFullCallStack ? int.MaxValue : maximumFramesToCapture;
        }

        public bool Enabled { get; }

        public int MaximumFramesToCapture { get; }

        public bool CaptureFullCallStack { get; }

        public static ExceptionDebuggingSettings FromSource(IConfigurationSource source, IConfigurationTelemetry telemetry)
        {
            return new ExceptionDebuggingSettings(source, telemetry);
        }

        public static ExceptionDebuggingSettings FromDefaultSource()
        {
            return FromSource(GlobalConfigurationSource.Instance, TelemetryFactory.Config);
        }
    }
}
