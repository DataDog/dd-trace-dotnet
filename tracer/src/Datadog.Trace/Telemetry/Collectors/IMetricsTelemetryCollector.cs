// <copyright file="IMetricsTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading.Tasks;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Telemetry;

internal partial interface IMetricsTelemetryCollector
{
    /// <summary>
    /// Called to record the usage of a public API by a customer
    /// </summary>
    /// <param name="api">The API that was accessed</param>
    public void Record(PublicApiUsage api);

    MetricResults GetMetrics();

    /// <summary>
    /// Sets the version of the WAF used for future metrics
    /// </summary>
    public void SetWafVersion(string wafVersion);

    public Task DisposeAsync();
}
