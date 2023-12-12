// <copyright file="ITelemetryTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.Telemetry
{
    internal interface ITelemetryTransport
    {
        /// <summary>
        /// Push telemetry data to the endpoint.
        /// </summary>
        /// <param name="data">The data to send</param>
        /// <returns><c>true</c> if the data was sent successfully, or a non-fatal error occurred
        /// <c>false</c> if a fatal error occured, and no further telemetry should be sent.</returns>
        Task<TelemetryPushResult> PushTelemetry(TelemetryData data);

        string GetTransportInfo();
    }
}
