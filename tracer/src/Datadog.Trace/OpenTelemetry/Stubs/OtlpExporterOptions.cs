// <copyright file="OtlpExporterOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    /// <summary>
    /// Minimal OtlpExporterOptions needed by the vendored gRPC export client.
    /// This avoids vendoring the full OpenTelemetry SDK types.
    /// </summary>
    internal sealed class OtlpExporterOptions
    {
        public Uri Endpoint { get; set; } = new("http://localhost:4317");

        public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public int TimeoutMilliseconds { get; set; } = 10000;

        public OtlpExportProtocol Protocol { get; set; } = OtlpExportProtocol.Grpc;

        public bool AppendSignalPathToEndpoint { get; set; } = true;

        public T GetHeaders<T>(Action<T, string, string> addHeader)
            where T : new()
        {
            var result = new T();
            foreach (var kvp in Headers)
            {
                addHeader(result, kvp.Key, kvp.Value);
            }

            return result;
        }
    }
}
#endif
