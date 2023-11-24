// <copyright file="IntegrationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Contains integration-specific settings.
/// </summary>
public sealed class IntegrationSettings
{
    private readonly bool? _enabledInitial;
    private readonly bool? _analyticsEnabledInitial;
    private readonly double _analyticsSampleRateInitial;
    private bool? _enabledOverride;
    private bool? _analyticsEnabledOverride;
    private double? _analyticsSampleRateOverride;

    internal IntegrationSettings(string integrationName, bool? enabled, bool? analyticsEnabled, double analyticsSampleRate)
    {
        IntegrationName = integrationName;
        _enabledInitial = enabled;
        _analyticsEnabledInitial = analyticsEnabled;
        _analyticsSampleRateInitial = analyticsSampleRate;
    }

    /// <summary>
    /// Gets the name of the integration. Used to retrieve integration-specific settings.
    /// </summary>
    internal string IntegrationName { get; }

    /// <summary>
    /// Gets or sets a value indicating whether
    /// this integration is enabled.
    /// </summary>
    internal bool? Enabled
    {
        get => _enabledOverride ?? _enabledInitial;
        set => _enabledOverride = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether
    /// Analytics are enabled for this integration.
    /// </summary>
    internal bool? AnalyticsEnabled
    {
        get => _analyticsEnabledOverride ?? _analyticsEnabledInitial;
        set => _analyticsEnabledOverride = value;
    }

    /// <summary>
    /// Gets or sets a value between 0 and 1 (inclusive)
    /// that determines the sampling rate for this integration.
    /// </summary>
    internal double AnalyticsSampleRate
    {
        get => _analyticsSampleRateOverride ?? _analyticsSampleRateInitial;
        set => _analyticsSampleRateOverride = value;
    }
}
