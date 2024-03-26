// <copyright file="ModuleInitializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;

namespace Datadog.Trace.Tests.Util
{
    public class ModuleInitializer
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            // disable telemetry for any tests which create a "real" telemetry instance
            Environment.SetEnvironmentVariable(ConfigurationKeys.Telemetry.Enabled, "false");

            // disable config by default
            TelemetryFactory.SetConfigForTesting(NullConfigurationTelemetry.Instance);

            // disable metrics by default
            TelemetryFactory.SetMetricsForTesting(NullMetricsTelemetryCollector.Instance);

            // Avoid race condition with modifying the formatters collection
            JTokenAssertions.Initialize();
        }
    }
}
