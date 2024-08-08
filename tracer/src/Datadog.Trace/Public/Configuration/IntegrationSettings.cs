// <copyright file="IntegrationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Configuration.IntegrationSettings;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Contains integration-specific settings.
/// </summary>
public sealed class IntegrationSettings
{
    private readonly string _integrationName;
    private OverrideValue<bool?> _enabled;
    private OverrideValue<bool?> _analyticsEnabled;
    private OverrideValue<double> _analyticsSampleRate;

    internal IntegrationSettings(string integrationName, bool? enabled, bool? analyticsEnabled, double analyticsSampleRate)
    {
        _integrationName = integrationName;
        _enabled = new(enabled);
        _analyticsEnabled = new(analyticsEnabled);
        _analyticsSampleRate = new(analyticsSampleRate);
    }

    /// <summary>
    /// Gets the name of the integration. Used to retrieve integration-specific settings.
    /// </summary>
    [Instrumented]
    public string IntegrationName
    {
        get
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                IntegrationNameGetIntegration.OnMethodBegin(this);
            }

            return _integrationName;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether
    /// this integration is enabled.
    /// </summary>
    public bool? Enabled
    {
        [Instrumented]
        get
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                EnabledGetIntegration.OnMethodBegin(this);
            }

            return _enabled.Value;
        }

        set => _enabled = _enabled.Override(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether
    /// Analytics are enabled for this integration.
    /// </summary>
    public bool? AnalyticsEnabled
    {
        [Instrumented]
        get
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                AnalyticsEnabledGetIntegration.OnMethodBegin(this);
            }

            return _analyticsEnabled.Value;
        }

        set => _analyticsEnabled = _analyticsEnabled.Override(value);
    }

    /// <summary>
    /// Gets or sets a value between 0 and 1 (inclusive)
    /// that determines the sampling rate for this integration.
    /// </summary>
    public double AnalyticsSampleRate
    {
        [Instrumented]
        get
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                AnalyticsSampleRateGetIntegration.OnMethodBegin(this);
            }

            return _analyticsSampleRate.Value;
        }

        set => _analyticsSampleRate = _analyticsSampleRate.Override(value);
    }

    /// <summary>
    /// "Serializes" the values, if changed, to an array. If there are no updates, returns null
    /// </summary>
    internal object?[]? GetChangeDetails()
        => IntegrationSettingsSerializationHelper.SerializeFromManual(
            _enabled.IsOverridden,
            _enabled.IsOverridden ? _enabled.Value : null,
            _analyticsEnabled.IsOverridden,
            _analyticsEnabled.IsOverridden ? _analyticsEnabled.Value : null,
            _analyticsSampleRate.IsOverridden,
            _analyticsSampleRate.IsOverridden ? _analyticsSampleRate.Value : null);
}
