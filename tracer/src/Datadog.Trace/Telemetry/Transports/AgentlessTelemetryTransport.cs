// <copyright file="AgentlessTelemetryTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Agent;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Telemetry.Transports;

internal class AgentlessTelemetryTransport : JsonTelemetryTransport
{
    public AgentlessTelemetryTransport(IApiRequestFactory requestFactory, bool debugEnabled)
        : base(requestFactory, debugEnabled)
    {
    }

    public override string GetTransportInfo() => nameof(AgentlessTelemetryTransport) + " to " + GetEndpointInfo();

    protected override MetricTags.TelemetryEndpoint GetEndpointMetricTag() => MetricTags.TelemetryEndpoint.Agentless;
}
