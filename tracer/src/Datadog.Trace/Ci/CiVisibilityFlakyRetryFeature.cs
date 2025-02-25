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

    public CiVisibilityFlakyRetryFeature(CIVisibilitySettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
    {
        settings ??= CIVisibilitySettings.FromDefaultSources();
        if (settings.ImpactedTestsDetectionEnabled != false && clientSettingsResponse.ImpactedTestsEnabled == true)
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

    public bool Enabled { get; }
}
