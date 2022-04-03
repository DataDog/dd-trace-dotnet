// <copyright file="BackendSnapshotApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Snapshots;

internal class BackendSnapshotApi : ISnapshotApi
{
    private const string SnpashotIntakePrefix = "https://http-intake.logs.";
    private const string SnapshotEndpoint = "/v1/input";

    private const string HeaderNameApiKey = "DD-API-KEY";
    private const string HeaderNameRuntimeId = "X-Datadog-HostId";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<BackendSnapshotApi>();

    private readonly IApiRequestFactory _apiRequestFactory;
    private readonly string _targetPath;
    private readonly string _apiKey;
    private readonly string _runtimeId;
    private Uri _uri;

    private BackendSnapshotApi(IApiRequestFactory apiRequestFactory, string targetPath, string apiKey, string runtimeId)
    {
        _apiRequestFactory = apiRequestFactory;
        _targetPath = targetPath;
        _apiKey = apiKey;
        _runtimeId = runtimeId;
    }

    public static BackendSnapshotApi Create(ImmutableDebuggerSettings settings, IApiRequestFactory apiRequestFactory)
    {
        var targetPath = settings.SnapshotsPath.StartsWith("http") ? settings.SnapshotsPath : $"{SnpashotIntakePrefix}{settings.SnapshotsPath}{SnapshotEndpoint}";
        return new BackendSnapshotApi(apiRequestFactory, targetPath, settings.ApiKey, settings.RuntimeId);
    }

    public async Task<bool> SendSnapshotsAsync(ArraySegment<byte> snapshots)
    {
        _uri ??= new Uri(_targetPath);

        var request = _apiRequestFactory.Create(_uri);
        request.AddHeader(HeaderNameApiKey, _apiKey);
        request.AddHeader(HeaderNameRuntimeId, _runtimeId);

        using var response = await request.PostAsync(snapshots, MimeTypes.Json).ConfigureAwait(false);

        if (response.StatusCode is not (>= 200 and <= 299))
        {
            var content = await response.ReadAsStringAsync().ConfigureAwait(false);
            Log.Warning<int, string>("Failed to get configurations with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, content);
            return false;
        }

        return true;
    }
}
