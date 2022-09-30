// <copyright file="DataStreamsApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Logging;

namespace Datadog.Trace.DataStreamsMonitoring.Transport;

internal class DataStreamsApi : IDataStreamsApi
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DataStreamsApi>();
    private readonly IApiRequestFactory _requestFactory;
    private readonly Uri _endpoint;

    public DataStreamsApi(IApiRequestFactory apiRequestFactory)
    {
        _requestFactory = apiRequestFactory;
        _endpoint = _requestFactory.GetEndpoint(DataStreamsConstants.IntakePath);
        Log.Debug("Using data streams intake endpoint {DataStreamsIntakeEndpoint}", _endpoint.ToString());
    }

    public async Task<bool> SendAsync(ArraySegment<byte> bytes)
    {
        try
        {
            Log.Debug<int>("Sending {Count} bytes to the data streams intake", bytes.Count);
            var request = _requestFactory.Create(_endpoint);

            using var response = await request.PostAsync(bytes, MimeTypes.MsgPack).ConfigureAwait(false);
            if (response.StatusCode is >= 200 and < 300)
            {
                Log.Debug("Data streams monitoring data sent successfully");
                return true;
            }

            Log.Warning<string, int>("Error sending data streams monitoring data to '{Endpoint}' {StatusCode} ", _requestFactory.Info(_endpoint), response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error sending data streams monitoring data to '{Endpoint}'", _requestFactory.Info(_endpoint));
            return false;
        }
    }
}
