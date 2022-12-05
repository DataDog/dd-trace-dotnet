// <copyright file="ITelemetryLogsSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.Telemetry;

internal interface ITelemetryLogsSink
{
    /// <summary>
    /// Need to initialize the sink _before_ we have access to these
    /// So we provide these only when we create telemetry
    /// </summary>
    void Initialize(
        ApplicationTelemetryData applicationData,
        HostTelemetryData hostData,
        TelemetryDataBuilder dataBuilder,
        ITelemetryTransport[] transports);

    void EnqueueLog(LogMessageData logEvent);

    Task DisposeAsync();

    /// <summary>
    /// Disables the sink entirely, drops any queued logs, and stops flushing
    /// Does not attempt to flush any logs.
    /// Note that the sink cannot be re-enabled after closing
    /// </summary>
    void CloseImmediately();
}
