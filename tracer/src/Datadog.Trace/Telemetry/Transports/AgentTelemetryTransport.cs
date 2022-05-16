// <copyright file="AgentTelemetryTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Agent;

namespace Datadog.Trace.Telemetry.Transports;

internal class AgentTelemetryTransport : JsonTelemetryTransport
{
    public AgentTelemetryTransport(IApiRequestFactory requestFactory)
        : base(requestFactory)
    {
    }

    public override string GetTransportInfo() => nameof(AgentTelemetryTransport) + " to " + GetEndpointInfo();
}
