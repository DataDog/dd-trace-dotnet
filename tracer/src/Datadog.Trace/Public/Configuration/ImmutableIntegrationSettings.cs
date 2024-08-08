// <copyright file="ImmutableIntegrationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Configuration.ImmutableIntegrationSettings;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Contains integration-specific settings.
/// </summary>
public sealed class ImmutableIntegrationSettings
{
    private readonly string _integrationName;
    private readonly bool? _enabled;
    private readonly bool? _analyticsEnabled;
    private readonly double _analyticsSampleRate;

    internal ImmutableIntegrationSettings(string name, bool? enabled, bool? analyticsEnabled, double analyticsSampleRate)
    {
        _integrationName = name;
        _enabled = enabled;
        _analyticsEnabled = analyticsEnabled;
        _analyticsSampleRate = analyticsSampleRate;
    }

    internal ImmutableIntegrationSettings(string name)
    {
        _integrationName = name;
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
    /// Gets a value indicating whether
    /// this integration is enabled.
    /// </summary>
    [Instrumented]
    public bool? Enabled
    {
        get
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                EnabledGetIntegration.OnMethodBegin(this);
            }

            return _enabled;
        }
    }

    /// <summary>
    /// Gets a value indicating whether
    /// Analytics are enabled for this integration.
    /// </summary>
    [Instrumented]
    public bool? AnalyticsEnabled
    {
        get
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                AnalyticsEnabledGetIntegration.OnMethodBegin(this);
            }

            return _analyticsEnabled;
        }
    }

    /// <summary>
    /// Gets a value between 0 and 1 (inclusive)
    /// that determines the sampling rate for this integration.
    /// </summary>
    [Instrumented]
    public double AnalyticsSampleRate
    {
        get
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                AnalyticsSampleRateGetIntegration.OnMethodBegin(this);
            }

            return _analyticsSampleRate;
        }
    }
}
