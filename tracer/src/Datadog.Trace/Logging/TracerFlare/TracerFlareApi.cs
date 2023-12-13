// <copyright file="TracerFlareApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;

namespace Datadog.Trace.Logging.TracerFlare;

internal class TracerFlareApi
{
    private const string TracerFlareEndpoint = "tracer_flare/v1";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TracerFlareApi>();

    private readonly IApiRequestFactory _requestFactory;
    private readonly Uri _endpoint;

    public TracerFlareApi(IApiRequestFactory requestFactory)
    {
        _requestFactory = requestFactory;
        _endpoint = _requestFactory.GetEndpoint(TracerFlareEndpoint);
    }

    public static TracerFlareApi Create(ImmutableExporterSettings exporterSettings)
    {
        var requestFactory = AgentTransportStrategy.Get(
            exporterSettings,
            productName: "tracer_flare",
            tcpTimeout: TimeSpan.FromSeconds(30),
            AgentHttpHeaderNames.MinimalHeaders,
            () => new MinimalAgentHeaderHelper(),
            uri => uri);

        return new TracerFlareApi(requestFactory);
    }

    public async Task SendTracerFlare(ArraySegment<byte> flare, string caseId)
    {
        try
        {
            Log.Debug("Sending {FlareSize} byte tracer flare to {Endpoint}", flare.Count, _endpoint);

            // TODO: Stream this instead
            var buffer = TracerFlareRequestFactory.GetRequestBody(flare, caseId);

            var request = _requestFactory.Create(_endpoint);
            using var response = await request.PostAsync(buffer, MimeTypes.MultipartFormData).ConfigureAwait(false);
            if (response.StatusCode is >= 200 and < 300)
            {
                Log.Debug("Tracer flare sent successfully");
                return;
            }

            Log.Warning<string, int>("Error sending tracer flare to '{Endpoint}' {StatusCode} ", _requestFactory.Info(_endpoint), response.StatusCode);
        }
        catch (Exception ex)
        {
            Log.Information(ex, "Error sending tracer flare to '{Endpoint}'", _requestFactory.Info(_endpoint));
        }
    }
}
