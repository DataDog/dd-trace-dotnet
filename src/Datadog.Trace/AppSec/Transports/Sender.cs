// <copyright file="Sender.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Abstractions;
using Datadog.Trace.Agent;
using Datadog.Trace.AppSec.EventModel.Batch;

namespace Datadog.Trace.AppSec.Transports
{
    internal class Sender
    {
        internal const string AppSecHeaderValue = "v0.1.0";
        internal const string AppSecHeaderKey = "X-Api-Version";
        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly Uri _uri;

        internal Sender()
        {
            _apiRequestFactory = Api.CreateRequestFactory();
            var settings = Tracer.Instance.Settings;
            // todo: read from configuration key
            _uri = new Uri(settings.AgentUri, "appsec");
        }

        internal Task Send(IEnumerable<IEvent> events)
        {
            var batch = new Intake()
            {
                Events = events,
                IdemPotencyKey = Guid.NewGuid().ToString()
            };
            var request = _apiRequestFactory.Create(_uri);
            request.AddHeader(AppSecHeaderKey, AppSecHeaderValue);
            return request.PostAsJsonAsync(batch);
        }
    }
}
