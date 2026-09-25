// <copyright file="ModuleInitializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Security.Unit.Tests.Utils;

public class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // disable telemetry for any tests which create a "real" telemetry instance
        Environment.SetEnvironmentVariable(ConfigurationKeys.Telemetry.Enabled, "false");

        TelemetryFactory.SetConfigForTesting(NullConfigurationTelemetry.Instance);
        TelemetryFactory.SetMetricsForTesting(NullMetricsTelemetryCollector.Instance);
    }
}
