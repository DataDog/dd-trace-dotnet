// <copyright file="AgentWriterHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Agent;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.TestHelpers.Stats;

namespace Datadog.Trace.TestHelpers.TestTracer;

internal static class AgentWriterHelper
{
    /// <summary>
    /// Creates an <see cref="AgentWriter"/> for tests that drive flushing themselves.
    /// </summary>
    public static AgentWriter CreateWithManualFlush(
        IApi api,
        IStatsAggregator statsAggregator = null,
        IStatsdManager statsd = null,
        int maxBufferSize = 1024 * 1024 * 10,
        bool initialTracerMetricsEnabled = false)
        => new(
            api,
            statsAggregator,
            statsd ?? TestStatsdManager.NoOp,
            automaticFlush: false,
            maxBufferSize,
            batchInterval: 0,
            initialTracerMetricsEnabled: initialTracerMetricsEnabled);
}
