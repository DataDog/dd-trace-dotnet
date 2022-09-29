// <copyright file="DataStreamsTransportStrategy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.DataStreamsMonitoring.Transport;

internal static class DataStreamsTransportStrategy
{
    public static IApiRequestFactory GetAgentIntakeFactory(ImmutableExporterSettings settings)
        => AgentTransportStrategy.Get(
            settings,
            productName: "data streams monitoring",
            tcpTimeout: TimeSpan.FromSeconds(5), // Short timeout, because we don't want to get "overlapping" flushes if the agent is being slow
            DataStreamsHttpHeaderNames.GetDefaultAgentHeaders(),
            () => new DataStreamsHttpHeaderHelper(),
            uri => uri);
}
