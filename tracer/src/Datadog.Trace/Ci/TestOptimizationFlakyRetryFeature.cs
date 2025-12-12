// <copyright file="TestOptimizationFlakyRetryFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci;

internal sealed class TestOptimizationFlakyRetryFeature : ITestOptimizationFlakyRetryFeature
{
    public const int FlakyRetryCountDefault = 0;
    public const int TotalFlakyRetryCountDefault = 0;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestOptimizationFlakyRetryFeature));

    private TestOptimizationFlakyRetryFeature(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
    {
        if (settings.FlakyRetryEnabled == null && clientSettingsResponse.FlakyTestRetries.HasValue)
        {
            Log.Information("TestOptimizationFlakyRetryFeature: Flaky retries has been changed to {Value} by the settings api.", clientSettingsResponse.FlakyTestRetries.Value);
            settings.SetFlakyRetryEnabled(clientSettingsResponse.FlakyTestRetries.Value);
        }

        if (settings.FlakyRetryEnabled == true)
        {
            Log.Information("TestOptimizationFlakyRetryFeature: Flaky retries is enabled.");
            Enabled = true;
        }
        else
        {
            Log.Information("TestOptimizationFlakyRetryFeature: Flaky retries is disabled.");
            Enabled = false;
        }

        FlakyRetryCount = settings.FlakyRetryCount;
        TotalFlakyRetryCount = settings.TotalFlakyRetryCount;
    }

    public bool Enabled { get; }

    public int FlakyRetryCount { get; }

    public int TotalFlakyRetryCount { get; }

    public static ITestOptimizationFlakyRetryFeature Create(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
        => new TestOptimizationFlakyRetryFeature(settings, clientSettingsResponse, testOptimizationClient);
}
