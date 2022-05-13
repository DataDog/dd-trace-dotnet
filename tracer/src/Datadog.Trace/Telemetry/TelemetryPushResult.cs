// <copyright file="TelemetryPushResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Telemetry
{
    /// <summary>
    /// The result of attempting to push telemetry to the agent
    /// </summary>
    internal enum TelemetryPushResult
    {
        Success,
        TransientFailure,
        FatalError,
        FatalErrorDontRetry,
    }
}
