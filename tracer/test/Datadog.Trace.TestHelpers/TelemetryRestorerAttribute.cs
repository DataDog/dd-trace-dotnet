// <copyright file="TelemetryRestorerAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers;

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public class TelemetryRestorerAttribute : BeforeAfterTestAttribute
{
    private IMetricsTelemetryCollector _metrics;
    private IConfigurationTelemetry _config;

    public override void Before(MethodInfo methodUnderTest)
    {
        _metrics = TelemetryFactory.Metrics;
        _config = TelemetryFactory.Config;
        base.Before(methodUnderTest);
    }

    public override void After(MethodInfo methodUnderTest)
    {
        TelemetryFactory.SetMetricsForTesting(_metrics);
        TelemetryFactory.SetConfigForTesting(_config);
        base.After(methodUnderTest);
    }
}
