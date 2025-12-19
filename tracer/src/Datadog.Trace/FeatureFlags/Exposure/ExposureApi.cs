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
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.Exposure.Model;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Exposure
{
    internal sealed class ExposureApi : IDisposable
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ExposureApi));

        public const string ExposurePath = "api/v2/exposure";
        private readonly TaskCompletionSource<bool> _processExit = new();
        private readonly TimeSpan _sendInterval = TimeSpan.FromSeconds(10);
        private readonly Queue<ExposureEvent> _exposures = new Queue<ExposureEvent>();
        private IApiRequestFactory? _apiRequestFactory = null;
        private Dictionary<string, string> _context = new Dictionary<string, string>();
        private int _started = 0;

        private ExposureApi(TracerSettings tracerSettings)
        {
            UpdateSettings(tracerSettings.Manager.InitialExporterSettings, tracerSettings.Manager.InitialMutableSettings);
            tracerSettings.Manager.SubscribeToChanges(settingChanges => UpdateSettings(settingChanges.UpdatedExporter ?? settingChanges.PreviousExporter, settingChanges.UpdatedMutable ?? settingChanges.PreviousMutable));
        }

        private void UpdateSettings(ExporterSettings exporterSettings, MutableSettings settings)
        {
            Log.Debug("ExposureApi::UpdateSettings -> Applyting settings");
            var apiRequestFactory = AgentTransportStrategy.Get(
                exporterSettings,
                productName: "FeatureFlags exposure",
                tcpTimeout: TimeSpan.FromSeconds(5),
                AgentHttpHeaderNames.MinimalHeaders,
                () => new MinimalAgentHeaderHelper(),
                uri => uri);
            Interlocked.Exchange(ref _apiRequestFactory, apiRequestFactory);

            var context = new Dictionary<string, string>
            {
                { "service", settings.ServiceName ?? "unknown" },
                { "env", settings.Environment ?? "unknown" },
                { "version", settings.ServiceVersion ?? "unknown" }
            };
            Interlocked.Exchange(ref _context, context);
        }

        public static ExposureApi Create(TracerSettings tracerSettings)
        {
            return new ExposureApi(tracerSettings);
        }

        public void Dispose()
        {
            try { _processExit.TrySetResult(true); }
            catch { }
        }

        public void TryToStartSendLoopIfNotStarted()
        {
            if (_started != 0 || Interlocked.Exchange(ref _started, 1) != 0)
            {
                return;
            }

            _ = Task.Run(SendLoopAsync).ContinueWith(t => { Log.Error(t.Exception, "FeatureFlags Exposure send loop failed"); }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task SendLoopAsync()
        {
            Log.Debug("ExposureApi::SendLoopAsync -> Enter");
            while (!_processExit.Task.IsCompleted)
            {
                try
                {
                    var apiRequestFactory = _apiRequestFactory;
                    if (apiRequestFactory is not null)
                    {
                        var uri = apiRequestFactory.GetEndpoint($"evp_proxy/v2/{ExposurePath}");
                        if (_exposures.Count > 0)
                        {
                            await SendBatchAsync(apiRequestFactory, uri).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error while sending Feature Flags exposures to the agent");
                }

                try
                {
                    await Task.WhenAny(_processExit.Task, Task.Delay(_sendInterval)).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // We are shutting down, so don't do anything about it
                }

                Log.Debug("ExposureApi::SendLoopAsync -> Exit");
            }
        }

        private async Task SendBatchAsync(IApiRequestFactory apiRequestFactory, Uri uri)
        {
            try
            {
                var request = apiRequestFactory.Create(uri);
                var payload = GetPayload();
                using var response = await request.PostAsync(payload, MimeTypes.Json)
                                        .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in Feature Flags exposures reporting loop");
            }
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

        public void SendExposure(in ExposureEvent exposure)
        {
            lock (_exposures)
            {
                _exposures.Enqueue(exposure);
                TryToStartSendLoopIfNotStarted();
            }
        }

        private sealed class ExposuresRequest(Dictionary<string, string> context, List<ExposureEvent> exposures)
        {
            public Dictionary<string, string> Context { get; } = context;

            public List<ExposureEvent> Exposures { get; } = exposures;
        }
    }
}
