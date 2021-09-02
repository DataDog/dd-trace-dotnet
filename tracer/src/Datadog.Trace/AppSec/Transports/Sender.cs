// <copyright file="Sender.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.AppSec.EventModel.Batch;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Transports
{
    internal class Sender
    {
        internal const string AppSecHeaderValue = "v0.1.0";
        internal const string AppSecHeaderKey = "X-Api-Version";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Sender>();
        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly Uri _uri;
        private readonly JsonSerializer _serializer;

        internal Sender()
        {
            _apiRequestFactory = TransportStrategy.Get(TracerSettings.FromDefaultSources()) ?? Api.CreateRequestFactory();
            var settings = Tracer.Instance.Settings;
            // todo: read from configuration key?
            _uri = new Uri(settings.AgentUri, "appsec/proxy/api/v2/appsecevts");
            _serializer = JsonSerializer.CreateDefault();
        }

        internal async Task Send(IEnumerable<IEvent> events)
        {
            var batch = new Intake()
            {
                Events = events,
                IdemPotencyKey = Guid.NewGuid().ToString()
            };
            var request = _apiRequestFactory.Create(_uri);
            request.AddHeader(AppSecHeaderKey, AppSecHeaderValue);
            var response = await request.PostAsJsonAsync(batch, _serializer);
            if (response.StatusCode != 200 && response.StatusCode != 202)
            {
                var responseText = await response.ReadAsStringAsync();
                Log.Warning($"AppSec event not correctly sent to backend {response.StatusCode} with response {responseText}");
            }
        }
    }
}
