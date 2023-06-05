// <copyright file="TelemetryInput.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Telemetry;

internal readonly struct TelemetryInput
{
    public TelemetryInput(
        ICollection<ConfigurationKeyValue>? configuration,
        ICollection<DependencyTelemetryData>? dependencies,
        ICollection<IntegrationTelemetryData>? integrations,
        MetricResults? metrics,
        ProductsData? products)
    : this(configuration, dependencies, integrations, metrics?.Metrics, metrics?.Distributions, products)
    {
    }

    public TelemetryInput(
        ICollection<ConfigurationKeyValue>? configuration,
        ICollection<DependencyTelemetryData>? dependencies,
        ICollection<IntegrationTelemetryData>? integrations,
        ICollection<MetricData>? metrics,
        ICollection<DistributionMetricData>? distributions,
        ProductsData? products)
    {
        Configuration = configuration;
        Dependencies = dependencies;
        Integrations = integrations;
        Metrics = metrics;
        Distributions = distributions;
        Products = products;
    }

    public ICollection<ConfigurationKeyValue>? Configuration { get; }

    public ICollection<DependencyTelemetryData>? Dependencies { get; }

    public ICollection<IntegrationTelemetryData>? Integrations { get; }

    public ICollection<MetricData>? Metrics { get; }

    public ICollection<DistributionMetricData>? Distributions { get; }

    public ProductsData? Products { get; }
}
