// <copyright file="NullTelemetryLogsSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.Telemetry;

internal class NullTelemetryLogsSink : ITelemetryLogsSink
{
    public static readonly NullTelemetryLogsSink Instance = new();

    public void Initialize(ApplicationTelemetryData applicationData, HostTelemetryData hostData, TelemetryDataBuilder dataBuilder, ITelemetryTransport[] transports)
    {
    }

    public void EnqueueLog(LogMessageData logEvent)
    {
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void CloseImmediately()
    {
    }
}
