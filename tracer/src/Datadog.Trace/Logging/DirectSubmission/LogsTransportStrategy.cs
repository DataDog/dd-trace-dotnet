// <copyright file="LogsTransportStrategy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.StreamFactories;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.HttpOverStreams;

namespace Datadog.Trace.Logging.DirectSubmission
{
    internal static class LogsTransportStrategy
    {
        public const string Http = "HTTP";
        public const string Tcp = "TCP";
        public const string Default = Http;
        public static readonly string[] ValidTransports = { Http, Tcp };

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Tracer>();

        public static IApiRequestFactory Get(DirectLogSubmissionSettings settings)
        {
            switch (settings.Transport)
            {
                case Tcp:
                    // TODO: Support TCP, including TLS etc
                    throw new InvalidOperationException("TCP is not currently supported for direct log submission");
                    // Log.Information("Using {FactoryType} for log submission transport.", nameof(TcpStreamFactory));
                    // return new HttpStreamRequestFactory(new TcpStreamFactory(settings.IntakeUrl.Host, settings.IntakeUrl.Port), new DatadogHttpClient());
                case Http:
                default:
#if NETCOREAPP
                    Log.Information("Using {FactoryType} for log submission transport.", nameof(HttpClientRequestFactory));
                    return new HttpClientRequestFactory();
#else
                    Log.Information("Using {FactoryType} for log submission transport.", nameof(ApiWebRequestFactory));
                    return new ApiWebRequestFactory();
#endif
            }
        }
    }
}
