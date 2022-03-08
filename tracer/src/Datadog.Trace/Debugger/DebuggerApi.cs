// <copyright file="DebuggerApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger;

internal class DebuggerApi
{
    public const string SnapshotsPath = "/debugger/v1/input";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DebuggerApi>();

    private readonly IApiRequestFactory _apiRequestFactory;
    private readonly Uri _snapshotsEndpoint;

    private DebuggerApi(Uri agentUri, IApiRequestFactory apiRequestFactory)
    {
        _snapshotsEndpoint = new Uri(agentUri, SnapshotsPath);
        _apiRequestFactory = apiRequestFactory;
    }

    public static DebuggerApi Create(Uri agentUri, IApiRequestFactory apiRequestFactory)
    {
        return new DebuggerApi(agentUri, apiRequestFactory);
    }

    public async Task<bool> SendSnapshotsAsync(ArraySegment<byte> snapshots, int numberOfSnapshots)
    {
        IApiResponse response = null;
        try
        {
            var request = _apiRequestFactory.Create(_snapshotsEndpoint);
            response = await request.PostAsync(snapshots, MimeTypes.Json).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"An error occurred while sending snapshots to agent at {_apiRequestFactory.Info(_snapshotsEndpoint)}");
            return false;
        }
        finally
        {
            response?.Dispose();
        }
    }
}
