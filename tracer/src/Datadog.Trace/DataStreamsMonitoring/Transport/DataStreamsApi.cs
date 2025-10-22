// <copyright file="DataStreamsApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.DataStreamsMonitoring.Transport;

internal class DataStreamsApi : IDataStreamsApi
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DataStreamsApi>();
    private RequestDetails _config;

    public DataStreamsApi(
        TracerSettings.SettingsManager settings,
        Func<ExporterSettings, IApiRequestFactory> factory)
    {
        UpdateFactory(settings.InitialExporterSettings);
        settings.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is not null)
            {
                UpdateFactory(changes.UpdatedExporter);
            }
        });

        [MemberNotNull(nameof(_config))]
        void UpdateFactory(ExporterSettings exporter)
        {
            var requestFactory = factory(exporter);
            var endpoint = requestFactory.GetEndpoint(DataStreamsConstants.IntakePath);
            Interlocked.Exchange(ref _config!, new(requestFactory, endpoint));

            Log.Debug("Using data streams intake endpoint {DataStreamsIntakeEndpoint}", endpoint);
        }
    }

    public async Task<bool> SendAsync(ArraySegment<byte> bytes)
    {
        var config = Volatile.Read(ref _config);
        var requestFactory = config.RequestFactory;
        try
        {
            Log.Debug<int>("Sending {Count} bytes to the data streams intake", bytes.Count);
            var request = requestFactory.Create(config.Endpoint);

            using var response = await request.PostAsync(bytes, MimeTypes.MsgPack, "gzip").ConfigureAwait(false);
            if (response.StatusCode is >= 200 and < 300)
            {
                Log.Debug("Data streams monitoring data sent successfully");
                return true;
            }

            var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
            Log.Warning<string, int, string>("Error sending data streams monitoring data to '{Endpoint}' {StatusCode} {Content}", requestFactory.Info(config.Endpoint), response.StatusCode, responseContent);
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error sending data streams monitoring data to '{Endpoint}'", requestFactory.Info(config.Endpoint));
            return false;
        }
    }

    private record RequestDetails(IApiRequestFactory RequestFactory, Uri Endpoint);
}
