// <copyright file="IntegrationSettingsKeys.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Configuration;

internal readonly ref struct IntegrationSettingsKeys
{
    public readonly string EnabledKey;
    public readonly string EnabledFallbackKey;
    public readonly string AnalyticsEnabledKey;
    public readonly string AnalyticsEnabledFallbackKey;
    public readonly string AnalyticsSampleRateKey;
    public readonly string AnalyticsSampleRateFallbackKey;

    public IntegrationSettingsKeys(string enabledKey, string enabledFallbackKey, string analyticsEnabledKey, string analyticsEnabledFallbackKey, string analyticsSampleRateKey, string analyticsSampleRateFallbackKey)
    {
        EnabledKey = enabledKey;
        EnabledFallbackKey = enabledFallbackKey;
        AnalyticsEnabledKey = analyticsEnabledKey;
        AnalyticsEnabledFallbackKey = analyticsEnabledFallbackKey;
        AnalyticsSampleRateKey = analyticsSampleRateKey;
        AnalyticsSampleRateFallbackKey = analyticsSampleRateFallbackKey;
    }
}
