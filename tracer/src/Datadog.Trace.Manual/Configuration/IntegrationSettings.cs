// <copyright file="IntegrationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
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
    private OverrideValue<bool?> _enabledOverride = new();
    private OverrideValue<bool?> _analyticsEnabledOverride = new();
    private OverrideValue<double> _analyticsSampleRateOverride = new();

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
    [Instrumented]
    public string IntegrationName { get; }

    /// <summary>
    /// Gets or sets a value indicating whether
    /// this integration is enabled.
    /// </summary>
    public bool? Enabled
    {
        [Instrumented]
        get => _enabledOverride.IsOverridden ? _enabledOverride.Value : _enabledInitial;
        set => _enabledOverride = new(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether
    /// Analytics are enabled for this integration.
    /// </summary>
    public bool? AnalyticsEnabled
    {
        [Instrumented]
        get => _analyticsEnabledOverride.IsOverridden ? _analyticsEnabledOverride.Value : _analyticsEnabledInitial;
        set => _analyticsEnabledOverride = new(value);
    }

    /// <summary>
    /// Gets or sets a value between 0 and 1 (inclusive)
    /// that determines the sampling rate for this integration.
    /// </summary>
    public double AnalyticsSampleRate
    {
        [Instrumented]
        get => _analyticsSampleRateOverride.IsOverridden ? _analyticsSampleRateOverride.Value : _analyticsSampleRateInitial;
        set => _analyticsSampleRateOverride = new(value);
    }

    /// <summary>
    /// "Serializes" the values, if changed, to an array. If there are no updates, returns null
    /// </summary>
    internal object?[]? GetChangeDetails()
        => IntegrationSettingsSerializationHelper.SerializeFromManual(
            _enabledOverride.IsOverridden,
            _enabledOverride.IsOverridden ? _enabledOverride.Value : null,
            _analyticsEnabledOverride.IsOverridden,
            _analyticsEnabledOverride.IsOverridden ? _analyticsEnabledOverride.Value : null,
            _analyticsSampleRateOverride.IsOverridden,
            _analyticsSampleRateOverride.IsOverridden ? _analyticsSampleRateOverride.Value : null);
}
