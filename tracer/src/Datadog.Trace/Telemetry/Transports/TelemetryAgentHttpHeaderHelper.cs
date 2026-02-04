// <copyright file="TelemetryAgentHttpHeaderHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.HttpOverStreams;

namespace Datadog.Trace.Telemetry.Transports
{
    internal sealed class TelemetryAgentHttpHeaderHelper : HttpHeaderHelperBase
    {
        protected override string MetadataHeaders => TelemetryHttpHeaderNames.HttpSerializedDefaultAgentHeaders;

        protected override string ContentType => "application/json";
    }
}
