// <copyright file="TracerFlareApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Logging.TracerFlare;

internal class TracerFlareApi
{
    internal const string TracerFlareSentLog = "Tracer flare sent successfully";
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

    public async Task<KeyValuePair<bool, string?>> SendTracerFlare(Func<Stream, Task> writeFlareToStreamFunc, string caseId, string hostname, string email)
    {
        try
        {
            Log.Debug("Sending tracer flare to {Endpoint}", _endpoint);

            var request = _requestFactory.Create(_endpoint);
            using var response = await request.PostAsync(
                                                   stream => TracerFlareRequestFactory.WriteRequestBody(stream, writeFlareToStreamFunc, caseId, hostname: hostname, email: email),
                                                   MimeTypes.MultipartFormData,
                                                   contentEncoding: null,
                                                   multipartBoundary: TracerFlareRequestFactory.Boundary)
                                              .ConfigureAwait(false);

            if (response.StatusCode is >= 200 and < 300)
            {
                Log.Information(TracerFlareSentLog);
                return new(true, null);
            }

            string? responseContent = null;
            string? error = null;
            try
            {
                responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                if (responseContent is not null)
                {
                    error = JObject.Parse(responseContent)["error"]?.ToString();
                }
            }
            catch (Exception e)
            {
                Log.Warning<int, string?>(e, "Error parsing {StatusCode} response from tracer flare endpoint: {ResponseContent}", response.StatusCode, responseContent);
            }

            Log.Warning<string, int>("Error sending tracer flare to '{Endpoint}' {StatusCode} ", _requestFactory.Info(_endpoint), response.StatusCode);
            return new(false, error);
        }
        catch (Exception ex)
        {
            Log.Information(ex, "Error sending tracer flare to '{Endpoint}'", _requestFactory.Info(_endpoint));
            return new(false, null);
        }
    }
}
