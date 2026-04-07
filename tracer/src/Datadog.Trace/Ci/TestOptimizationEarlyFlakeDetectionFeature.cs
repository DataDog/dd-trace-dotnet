// <copyright file="TestOptimizationEarlyFlakeDetectionFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci;

internal sealed class TestOptimizationEarlyFlakeDetectionFeature : ITestOptimizationEarlyFlakeDetectionFeature
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestOptimizationEarlyFlakeDetectionFeature));

    private TestOptimizationEarlyFlakeDetectionFeature(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse)
    {
        EarlyFlakeDetectionSettings = clientSettingsResponse.EarlyFlakeDetection;

        if ((settings.EarlyFlakeDetectionEnabled == true || clientSettingsResponse.EarlyFlakeDetection.Enabled == true) && settings.KnownTestsEnabled == true)
        {
            Log.Information("TestOptimizationEarlyFlakeDetectionFeature: Early flake detection is enabled.");
            settings.SetEarlyFlakeDetectionEnabled(true);
            Enabled = true;
        }
        else
        {
            Log.Information("TestOptimizationEarlyFlakeDetectionFeature: Early flake detection is disabled.");
            settings.SetEarlyFlakeDetectionEnabled(false);
            Enabled = false;
        }
    }

    public bool Enabled { get; }

    public TestOptimizationClient.EarlyFlakeDetectionSettingsResponse EarlyFlakeDetectionSettings { get; }

    public static ITestOptimizationEarlyFlakeDetectionFeature Create(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse)
        => new TestOptimizationEarlyFlakeDetectionFeature(settings, clientSettingsResponse);
}
