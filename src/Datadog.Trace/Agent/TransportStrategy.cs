using Datadog.Trace.Agent.StreamFactories;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent
{
    internal static class TransportStrategy
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<Tracer>();

        public static IApiRequestFactory Get(TracerSettings settings)
        {
            var strategy = settings.TracesTransport?.ToUpperInvariant();

            switch (strategy)
            {
                case "DATADOG-TCP":
                    Log.Information("Using {0} for trace transport.", nameof(TcpStreamFactory));
                    return new HttpStreamRequestFactory(new TcpStreamFactory(settings.AgentUri.Host, settings.AgentUri.Port), new DatadogHttpClient());
                case "DATADOG-NAMED-PIPES":
                    Log.Information("Using {0} for trace transport, with pipe name {1} and timeout {2}.", nameof(NamedPipeClientStreamFactory), settings.TracesPipeName, settings.TracesPipeTimeoutMs);
                    return new HttpStreamRequestFactory(new NamedPipeClientStreamFactory(settings.TracesPipeName, settings.TracesPipeTimeoutMs), new DatadogHttpClient());
                default:
                    // Defer decision to Api logic
                    return null;
            }
        }
    }
}
