// <copyright file="ExposureApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;

namespace Datadog.Trace.FeatureFlags.Exposure;

internal sealed class ExposureApi : IDisposable
{
    internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ExposureApi));

    private const int DefaultCapacity = 1 << 16; // 65536 elements
    public const string ExposurePath = "evp_proxy/v2/api/v2/exposures";
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        NullValueHandling = NullValueHandling.Include,
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy(),
        }
    };

    private readonly TaskCompletionSource<bool> _processExit = new();
    private readonly TimeSpan _sendInterval = TimeSpan.FromSeconds(10);
    private readonly Queue<ExposureEvent> _exposures = new Queue<ExposureEvent>();

    private ExposureCache _exposureCache = new ExposureCache(DefaultCapacity);
    private IApiRequestFactory _apiRequestFactory;
    private Dictionary<string, string> _context;
    private int _started = 0;

    internal ExposureApi(TracerSettings tracerSettings)
    {
        UpdateApi(tracerSettings.Manager.InitialExporterSettings);
        UpdateContext(tracerSettings.Manager.InitialMutableSettings);

        tracerSettings.Manager.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is { } exporter)
            {
                UpdateApi(exporter);
            }

            if (changes.UpdatedMutable is { } mutable)
            {
                UpdateContext(mutable);
            }
        });

        [MemberNotNull(nameof(_apiRequestFactory))]
        void UpdateApi(ExporterSettings exporterSettings)
        {
            Log.Debug("ExposureApi::UpdateApi-> Applying settings");
            var apiRequestFactory = AgentTransportStrategy.Get(
                exporterSettings,
                productName: "FeatureFlags exposure",
                tcpTimeout: TimeSpan.FromSeconds(5),
                AgentHttpHeaderNames.MinimalHeaders,
                () => new MinimalAgentHeaderHelper(),
                uri => uri);
            Interlocked.Exchange(ref _apiRequestFactory!, apiRequestFactory);
        }

        [MemberNotNull(nameof(_context))]
        void UpdateContext(MutableSettings settings)
        {
            Log.Debug("ExposureApi::UpdateContext -> Applying settings");
            var context = new Dictionary<string, string>
            {
                { "service", settings.DefaultServiceName },
                { "env", settings.Environment ?? "unknown" },
                { "version", settings.ServiceVersion ?? "unknown" }
            };
            Interlocked.Exchange(ref _context!, context);
        }
    }

    public void Dispose()
    {
        _processExit.TrySetResult(true);
    }

    public void TryToStartSendLoopIfNotStarted()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
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
                var uri = apiRequestFactory.GetEndpoint(ExposurePath);
                var payload = TryGetPayload();
                if (payload is not null)
                {
                    var request = apiRequestFactory.Create(uri);
                    using var response = await request.PostAsJsonAsync(payload, MultipartCompression.None, SerializerSettings).ConfigureAwait(false);
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

    private ExposuresRequest? TryGetPayload()
    {
        List<ExposureEvent> exposures;
        lock (_exposures)
        {
            if (_exposures.Count == 0)
            {
                // nothing to do, skip send
                return null;
            }

            exposures = [.. _exposures];
            _exposures.Clear();
        }

        return new ExposuresRequest(_context, exposures);
    }

    public void SendExposure(in ExposureEvent exposure)
    {
        if (_exposureCache.Add(exposure))
        {
            lock (_exposures)
            {
                _exposures.Enqueue(exposure);
            }
        }

        TryToStartSendLoopIfNotStarted();
    }

    private sealed class ExposuresRequest(Dictionary<string, string> context, List<ExposureEvent> exposures)
    {
        public Dictionary<string, string> Context { get; } = context;

        public List<ExposureEvent> Exposures { get; } = exposures;
    }
}
