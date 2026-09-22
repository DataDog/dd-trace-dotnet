// <copyright file="TelemetryAgentHttpHeaderHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.HttpOverStreams;

namespace Datadog.Trace.Telemetry.Transports
{
    internal sealed class TelemetryAgentHttpHeaderHelper : HttpHeaderHelperBase
    {
        public static readonly TelemetryAgentHttpHeaderHelper Instance = new();

        private TelemetryAgentHttpHeaderHelper()
        {
        }

        public override KeyValuePair<string, string>[] DefaultHeaders => TelemetryHttpHeaderNames.GetDefaultAgentHeaders();

        protected override string HttpSerializedDefaultHeaders => TelemetryHttpHeaderNames.HttpSerializedDefaultAgentHeaders;
    }
}
