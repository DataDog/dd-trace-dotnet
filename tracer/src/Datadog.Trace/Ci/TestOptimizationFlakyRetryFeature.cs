// <copyright file="TestOptimizationFlakyRetryFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci;

internal sealed class TestOptimizationFlakyRetryFeature : ITestOptimizationFlakyRetryFeature
{
    public const int FlakyRetryCountDefault = 0;
    public const int TotalFlakyRetryCountDefault = 0;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestOptimizationFlakyRetryFeature));

    // Retry count for each duration bucket of GetDurationBucket, or null to use FlakyRetryCount for every test.
    private readonly int[]? _dynamicRetryCounts;

    private TestOptimizationFlakyRetryFeature(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse)
    {
        if (!settings.FlakyRetryEnabled.HasValue && clientSettingsResponse.FlakyTestRetries.HasValue)
        {
            Log.Information("TestOptimizationFlakyRetryFeature: Flaky retries has been changed to {Value} by the settings api.", clientSettingsResponse.FlakyTestRetries.Value);
            settings.SetFlakyRetryEnabled(clientSettingsResponse.FlakyTestRetries.Value);
        }

        Enabled = settings.FlakyRetryEnabled == true;
        FlakyRetryCount = settings.FlakyRetryCount;
        TotalFlakyRetryCount = settings.TotalFlakyRetryCount;
        Log.Information("{V}", Enabled ? "TestOptimizationFlakyRetryFeature: Flaky retries is enabled." : "TestOptimizationFlakyRetryFeature: Flaky retries is disabled.");

        if (Enabled && settings.DynamicFlakyRetryEnabled)
        {
            _dynamicRetryCounts = settings.DynamicFlakyRetryCounts ?? GetSlowTestRetryCounts(clientSettingsResponse.EarlyFlakeDetection.SlowTestRetries);
            if (_dynamicRetryCounts is { } counts)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityDynamicAtrRetriesEnabled(settings.DynamicFlakyRetryCounts is null ? MetricTags.CIVisibilityDynamicFlakyRetryBuckets.Default : MetricTags.CIVisibilityDynamicFlakyRetryBuckets.Custom);
                Log.Information<int, int, int, int, int>("TestOptimizationFlakyRetryFeature: Dynamic flaky retries is enabled. [RetryCounts: {Under5s}, {Under10s}, {Under30s}, {Under5m}, {Slower}]", counts[0], counts[1], counts[2], counts[3], counts[4]);
            }
            else
            {
                Log.Information("TestOptimizationFlakyRetryFeature: Dynamic flaky retries is disabled because the settings have no slow test retries.");
            }
        }
    }

    public bool Enabled { get; }

    public int FlakyRetryCount { get; }

    public int TotalFlakyRetryCount { get; }

    public static ITestOptimizationFlakyRetryFeature Create(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse)
        => new TestOptimizationFlakyRetryFeature(settings, clientSettingsResponse);

    public int GetFlakyRetryCount(TimeSpan firstExecutionDuration)
        => _dynamicRetryCounts?[GetDurationBucket(firstExecutionDuration)] ?? FlakyRetryCount;

    // The bucket limits are the Early Flake Detection ones in Common.GetNumberOfExecutionsForDuration. That method is not
    // shared because it skips buckets that the backend leaves empty, while dynamic retries use one count per bucket.
    private static int GetDurationBucket(TimeSpan duration)
        => duration.TotalSeconds switch
        {
            < 5 => 0,
            < 10 => 1,
            < 30 => 2,
            < 300 => 3,
            _ => 4,
        };

    // Without any slow test retries there is nothing to derive the counts from, so FlakyRetryCount stays. That is the case
    // for settings not fetched from the backend, such as in child processes of `dd-trace ci run` that skip the request.
    private static int[]? GetSlowTestRetryCounts(TestOptimizationClient.SlowTestRetriesSettingsResponse slowTestRetries)
    {
        if (slowTestRetries is { FiveSeconds: null, TenSeconds: null, ThirtySeconds: null, FiveMinutes: null })
        {
            return null;
        }

        // Every bucket keeps at least one retry, including tests slower than five minutes, which EFD never retries.
        return
        [
            Math.Max(slowTestRetries.FiveSeconds ?? 0, 1),
            Math.Max(slowTestRetries.TenSeconds ?? 0, 1),
            Math.Max(slowTestRetries.ThirtySeconds ?? 0, 1),
            Math.Max(slowTestRetries.FiveMinutes ?? 0, 1),
            1,
        ];
    }
}
