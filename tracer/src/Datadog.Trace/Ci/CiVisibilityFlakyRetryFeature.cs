// <copyright file="CiVisibilityFlakyRetryFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci;

internal class CiVisibilityFlakyRetryFeature : ICiVisibilityFlakyRetryFeature
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CiVisibilityFlakyRetryFeature));

    private CiVisibilityFlakyRetryFeature(CIVisibilitySettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
    {
        if (settings is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(settings));
        }

        if (testOptimizationClient is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(testOptimizationClient));
        }

        if (settings.FlakyRetryEnabled != false && clientSettingsResponse.FlakyTestRetries == true)
        {
            Log.Debug("CiVisibilityFlakyRetryFeature: Flaky retries is enabled.");
            settings.SetImpactedTestsEnabled(true);
            Enabled = true;
        }
        else
        {
            Log.Debug("CiVisibilityFlakyRetryFeature: Flaky retries is disabled.");
            settings.SetImpactedTestsEnabled(false);
            Enabled = false;
        }
    }

    private CiVisibilityFlakyRetryFeature()
    {
        Enabled = false;
    }

    public bool Enabled { get; }

    public static ICiVisibilityFlakyRetryFeature CreateDisabledFeature() => new CiVisibilityFlakyRetryFeature();

    public static ICiVisibilityFlakyRetryFeature Create(CIVisibilitySettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
        => new CiVisibilityFlakyRetryFeature(settings, clientSettingsResponse, testOptimizationClient);
}
