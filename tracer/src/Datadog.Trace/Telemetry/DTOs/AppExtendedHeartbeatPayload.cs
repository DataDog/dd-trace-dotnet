// <copyright file="AppExtendedHeartbeatPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;

namespace Datadog.Trace.Telemetry;

/// <summary>
/// This event will be used as a failsafe if there are any catastrophic data failure.
/// The data will be used to reconstruct application records in our db.
/// </summary>
internal class AppExtendedHeartbeatPayload : IPayload
{
    public ICollection<ConfigurationKeyValue>? Configuration { get; set; }

    public ICollection<DependencyTelemetryData>? Dependencies { get; set; }

    public ICollection<IntegrationTelemetryData>? Integrations { get; set; }
}
