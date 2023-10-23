// <copyright file="TelemetryData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Telemetry
{
    /// <summary>
    /// DTO that is serialized.
    /// Be aware that the property names control serialization
    /// </summary>
    internal class TelemetryData
    {
        public TelemetryData(
            string requestType,
            long tracerTime,
            string runtimeId,
            int seqId,
            ApplicationTelemetryData application,
            HostTelemetryData host,
            IPayload? payload)
        {
            RequestType = requestType;
            TracerTime = tracerTime;
            RuntimeId = runtimeId;
            SeqId = seqId;
            Application = application;
            Host = host;
            Payload = payload;
        }

        public string ApiVersion => TelemetryConstants.ApiVersionV2;

        /// <summary>
        /// Gets or sets integer denoting the naming schema version used by the tracer. Empty by default. Possible values are "0", "1"
        /// </summary>
        public string? NamingSchemaVersion { get; set; }

        /// <summary>
        /// Gets or sets requested API function
        /// </summary>
        public string RequestType { get; set; }

        /// <summary>
        /// Gets or sets unix timestamp (in seconds) of when the message is being sent
        /// </summary>
        public long TracerTime { get; set; }

        public string RuntimeId { get; set; }

        /// <summary>
        /// Gets or sets counter that should be auto incremented every time an API call is being made
        /// </summary>
        public int SeqId { get; set; }

        public ApplicationTelemetryData Application { get; set; }

        public HostTelemetryData Host { get; set; }

        public IPayload? Payload { get; set; }
    }
}
