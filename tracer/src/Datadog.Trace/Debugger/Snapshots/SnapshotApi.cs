// <copyright file="SnapshotApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Snapshots;

internal class SnapshotApi
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SnapshotApi>();

    private readonly IApiRequestFactory _apiRequestFactory;
    private readonly DiscoveryService _discoveryService;
    private readonly string _targetPath;
    private readonly ProbeMode _probeMode;
    private Uri _uri;

    private SnapshotApi(IApiRequestFactory apiRequestFactory, DiscoveryService discoveryService, string targetPath, ProbeMode probeMode)
    {
        _probeMode = probeMode;
        _apiRequestFactory = apiRequestFactory;
        _discoveryService = discoveryService;
        _targetPath = targetPath;
    }

    public static SnapshotApi Create(ImmutableDebuggerSettings settings, IApiRequestFactory apiRequestFactory, DiscoveryService discoveryService)
    {
        var snapshotPath = settings.SnapshotsPath;
        return new SnapshotApi(apiRequestFactory, discoveryService, snapshotPath, settings.ProbeMode);
    }

    public async Task<bool> SendSnapshotsAsync(ArraySegment<byte> snapshots, int numberOfSnapshots)
    {
        _uri ??=
            _probeMode == ProbeMode.Backend
                ? new Uri(_targetPath)
                : new Uri($"{_targetPath}/{_discoveryService.DebuggerEndpoint}");

        var request = _apiRequestFactory.Create(_uri);
        var response = await request.PostAsync(snapshots, MimeTypes.Json).ConfigureAwait(false);

        if (response.StatusCode is not (>= 200 and <= 299))
        {
            var content = await response.ReadAsStringAsync().ConfigureAwait(false);
            Log.Warning<int, string>("Failed to get configurations with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, content);
            return false;
        }

        return true;
    }
}
