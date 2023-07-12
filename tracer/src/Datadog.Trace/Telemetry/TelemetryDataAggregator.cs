// <copyright file="TelemetryDataAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Telemetry;

internal class TelemetryDataAggregator
{
    private TelemetryInput? _previous;
    private bool _appStartedSent;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelemetryDataAggregator"/> class. For testing only.
    /// </summary>
    internal TelemetryDataAggregator(TelemetryInput? previous)
    {
        _previous = previous;
    }

    /// <summary>
    /// Gets the configuration values that should be sent to telemetry, including any previous, retained, values
    /// </summary>
    /// <returns>The combined configuration values that should be sent, including data that previously failed to send</returns>
    public TelemetryInput Combine(
        ICollection<ConfigurationKeyValue>? configuration,
        ICollection<DependencyTelemetryData>? dependencies,
        ICollection<IntegrationTelemetryData>? integrations,
        in MetricResults? metrics,
        ProductsData? products)
    {
        return new TelemetryInput(
            CombineWith(configuration),
            CombineWith(dependencies),
            CombineWith(integrations),
            CombineWith(metrics?.Metrics),
            CombineWith(metrics?.Distributions),
            CombineWith(products),
            sendAppStarted: !_appStartedSent);
    }

    public void SaveDataIfRequired(
        TelemetryTransportResult retryResult,
        in TelemetryInput input)
    {
        switch (retryResult)
        {
            case TelemetryTransportResult.Success:
                // This assumes that if we have sent the data, we sent _all_ the data
                // This is true when we're ONLY ever sending single messages
                // (as we are currently, using message-batch), but needs to be
                // updated to be more granular if this changes
                _previous = null;
                if (input.SendAppStarted)
                {
                    _appStartedSent = true;
                }

                break;

            case TelemetryTransportResult.FatalError:
                // Fatal, just don't hold any reference, we're giving up
                _previous = null;
                break;

            case TelemetryTransportResult.TransientError:
                // We should retry using this data next time (if we have sent something)
                _previous = input;
                break;
        }
    }

    private ICollection<ConfigurationKeyValue>? CombineWith(ICollection<ConfigurationKeyValue>? newValues)
    {
        var previous = _previous?.Configuration;
        if (previous is null)
        {
            return newValues;
        }

        if (newValues is null)
        {
            return previous;
        }

        var updatedValues = new List<ConfigurationKeyValue>(newValues.Count + previous.Count);
        updatedValues.AddRange(previous); // We know there won't be any "duplicates" here
        updatedValues.AddRange(newValues);
        return updatedValues;
    }

    private ICollection<DependencyTelemetryData>? CombineWith(ICollection<DependencyTelemetryData>? newValues)
    {
        var previous = _previous?.Dependencies;
        if (previous is null)
        {
            return newValues;
        }

        if (newValues is null)
        {
            return previous;
        }

        var updatedValues = new List<DependencyTelemetryData>(newValues.Count + previous.Count);
        updatedValues.AddRange(previous); // We know there won't be any "duplicates" here
        updatedValues.AddRange(newValues);
        return updatedValues;
    }

    private ICollection<IntegrationTelemetryData>? CombineWith(ICollection<IntegrationTelemetryData>? newValues)
    {
        var previous = _previous?.Integrations;
        if (previous is null)
        {
            return newValues;
        }

        if (newValues is null)
        {
            return previous;
        }

        var updatedValues = new List<IntegrationTelemetryData>(newValues.Count + previous.Count);
        updatedValues.AddRange(newValues);

        foreach (var previousValue in previous)
        {
            if (newValues.Any(updatedValue => updatedValue.Name == previousValue.Name))
            {
                continue;
            }

            updatedValues.Add(previousValue);
        }

        return updatedValues;
    }

    private ICollection<MetricData>? CombineWith(ICollection<MetricData>? newValues)
    {
        var previous = _previous?.Metrics;
        if (previous is null)
        {
            return newValues;
        }

        if (newValues is null)
        {
            return previous;
        }

        var updatedValues = new List<MetricData>();
        updatedValues.AddRange(newValues);

        // Yeah, this kinda sucks, but it's happening in a separate thread once a minute
        // so shouldn't be a big issue
        foreach (var previousValue in previous)
        {
            var found = false;
            foreach (var newValue in newValues)
            {
                if (newValue.Metric == previousValue.Metric)
                {
                    // update the point that's already in the list
                    newValue.Points.AddRange(previousValue.Points);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                updatedValues.Add(previousValue);
            }
        }

        return updatedValues;
    }

    private ICollection<DistributionMetricData>? CombineWith(ICollection<DistributionMetricData>? newValues)
    {
        var previous = _previous?.Distributions;
        if (previous is null)
        {
            return newValues;
        }

        if (newValues is null)
        {
            return previous;
        }

        var updatedValues = new List<DistributionMetricData>();
        updatedValues.AddRange(newValues);

        // Yeah, this kinda sucks, but it's happening in a separate thread once a minute
        // so shouldn't be a big issue
        foreach (var previousValue in previous)
        {
            var found = false;
            foreach (var newValue in newValues)
            {
                if (newValue.Metric == previousValue.Metric)
                {
                    // update the point that's already in the list
                    newValue.Points.AddRange(previousValue.Points);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                updatedValues.Add(previousValue);
            }
        }

        return updatedValues;
    }

    private ProductsData? CombineWith(ProductsData? newValues)
    {
        var previous = _previous?.Products;
        if (previous is null)
        {
            return newValues;
        }

        if (newValues is null)
        {
            return previous;
        }

        return new ProductsData()
        {
            Appsec = newValues.Appsec ?? previous.Appsec,
            Profiler = newValues.Profiler ?? previous.Profiler,
            DynamicInstrumentation = newValues.DynamicInstrumentation ?? previous.DynamicInstrumentation,
        };
    }
}
