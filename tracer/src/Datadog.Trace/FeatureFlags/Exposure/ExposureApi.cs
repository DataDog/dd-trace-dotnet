// <copyright file="ExposureApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Exposure.Model;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Internal;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog;
using static InlineIL.IL;

namespace Datadog.Trace.Exposure
{
    internal class ExposureApi
    {
        public const string ExposurePath = "api/v2/exposure";
        private readonly TaskCompletionSource<bool> _processExit = new();
        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly Queue<ExposureEvent> _exposures = new Queue<ExposureEvent>();
        private readonly TimeSpan _sendInterval = TimeSpan.FromSeconds(10);
        private readonly Dictionary<string, string> _context = new Dictionary<string, string>();
        private int _started = 0;

        private ExposureApi(IApiRequestFactory apiRequestFactory, TracerSettings tracerSettings)
        {
            _apiRequestFactory = apiRequestFactory;
            _context["service"] = tracerSettings.Manager.InitialMutableSettings.ServiceName ?? "unknown";
            _context["env"] = tracerSettings.Manager.InitialMutableSettings.Environment ?? "unknown";
            _context["version"] = tracerSettings.Manager.InitialMutableSettings.ServiceVersion ?? "unknown";
        }

        public static ExposureApi Create(TracerSettings tracerSettings)
        {
            var apiRequestFactory = AgentTransportStrategy.Get(
                tracerSettings.Manager.InitialExporterSettings,
                productName: "FeatureFlags exposure",
                tcpTimeout: TimeSpan.FromSeconds(5),
                AgentHttpHeaderNames.MinimalHeaders,
                () => new MinimalAgentHeaderHelper(),
                uri => uri);

            return new ExposureApi(apiRequestFactory, tracerSettings);
        }

        public void Start()
        {
            _ = Task.Run(StartSendLoopAsync)
               .ContinueWith(t => { Log.Error(t.Exception, "FeatureFlags Exposure send loop failed"); }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task StartSendLoopAsync()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
            {
                return;
            }

            var uri = _apiRequestFactory.GetEndpoint($"evp_proxy/v2/{ExposurePath}");

            while (!_processExit.Task.IsCompleted)
            {
                if (_exposures.Count > 0)
                {
                    try
                    {
                        await SendBatchAsync(uri).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error while sending Feature Flags exposures to the agent");
                    }
                }

                try
                {
                    await Task.WhenAny(_processExit.Task, Task.Delay(_sendInterval)).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // We are shutting down, so don't do anything about it
                }
            }
        }

        private async Task SendBatchAsync(Uri uri)
        {
            var request = _apiRequestFactory.Create(uri);
            var payload = GetPayload();
            using var response = await request.PostAsync(payload, MimeTypes.Json)
                                    .ConfigureAwait(false);
        }

        private ArraySegment<byte> GetPayload()
        {
            ExposuresRequest request;
            lock (_exposures)
            {
                var exposures = _exposures.ToList();
                _exposures.Clear();
                request = new ExposuresRequest(_context, exposures);
            }

            string json = JsonConvert.SerializeObject(request);
            return new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
        }

        public void SendExposure(ExposureEvent exposure)
        {
            lock (_exposures)
            {
                _exposures.Enqueue(exposure);
                Start();
            }
        }

        private class ExposuresRequest(Dictionary<string, string> context, List<ExposureEvent> exposures)
        {
            public Dictionary<string, string> Context { get; } = context;

            public List<ExposureEvent> Exposures { get; } = exposures;
        }
    }
}
