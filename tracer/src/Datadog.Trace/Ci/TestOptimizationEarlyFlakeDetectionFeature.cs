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
    private readonly TestOptimizationSettings _settings;
    private readonly bool _enabled;

    private TestOptimizationEarlyFlakeDetectionFeature(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse)
    {
        _settings = settings;
        EarlyFlakeDetectionSettings = clientSettingsResponse.EarlyFlakeDetection;

        if ((settings.EarlyFlakeDetectionEnabled == true || clientSettingsResponse.EarlyFlakeDetection.Enabled == true) && settings.KnownTestsEnabled == true)
        {
            Log.Information("TestOptimizationEarlyFlakeDetectionFeature: Early flake detection is enabled.");
            settings.SetEarlyFlakeDetectionEnabled(true);
            _enabled = true;
        }
        else
        {
            Log.Information("TestOptimizationEarlyFlakeDetectionFeature: Early flake detection is disabled.");
            settings.SetEarlyFlakeDetectionEnabled(false);
            _enabled = false;
        }
    }

    public bool Enabled => _enabled && _settings.KnownTestsEnabled == true && _settings.EarlyFlakeDetectionEnabled == true;

    public TestOptimizationClient.EarlyFlakeDetectionSettingsResponse EarlyFlakeDetectionSettings { get; }

    public static ITestOptimizationEarlyFlakeDetectionFeature Create(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse)
        => new TestOptimizationEarlyFlakeDetectionFeature(settings, clientSettingsResponse);
}
