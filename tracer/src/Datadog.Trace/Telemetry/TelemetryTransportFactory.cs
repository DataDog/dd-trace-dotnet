// <copyright file="TelemetryTransportFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetryTransportFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TelemetryTransportFactory>();
        private readonly Uri _baseEndpoint;
        private readonly string _apiKey;

        public TelemetryTransportFactory(Uri baseEndpoint, string apiKey)
        {
            _baseEndpoint = baseEndpoint;
            _apiKey = apiKey;
        }

        public ITelemetryTransport Create()
        {
#if NETCOREAPP
            Log.Debug("Using {FactoryType} for telemetry transport.", nameof(JsonHttpClientTelemetryTransport));
            var httpClient = new System.Net.Http.HttpClient { BaseAddress = _baseEndpoint };
            return new JsonHttpClientTelemetryTransport(httpClient, _apiKey);
#else
            Log.Debug("Using {FactoryType} for telemetry transport.", nameof(JsonWebRequestTelemetryTransport));
            return new JsonWebRequestTelemetryTransport(_baseEndpoint, _apiKey);
#endif
        }
    }
}
