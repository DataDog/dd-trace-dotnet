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
        ProductsData? products,
        bool sendAppStarted)
    {
        Configuration = configuration;
        Dependencies = dependencies;
        Integrations = integrations;
        Metrics = metrics?.Metrics;
        Distributions = metrics?.Distributions;
        Products = products;
        SendAppStarted = sendAppStarted;
    }

    public bool SendAppStarted { get; }

    public ICollection<ConfigurationKeyValue>? Configuration { get; }

    public ICollection<DependencyTelemetryData>? Dependencies { get; }

    public ICollection<IntegrationTelemetryData>? Integrations { get; }

    public ICollection<MetricData>? Metrics { get; }

    public ICollection<DistributionMetricData>? Distributions { get; }

    public ProductsData? Products { get; }
}
