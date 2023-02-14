// <copyright file="DataStreamsManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring.Aggregation;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.DataStreamsMonitoring;

/// <summary>
/// Manages all the data streams monitoring behaviour
/// </summary>
internal class DataStreamsManager
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DataStreamsManager>();
    private readonly NodeHashBase _nodeHashBase;
    private bool _isEnabled;
    private IDataStreamsWriter? _writer = null;

    public DataStreamsManager(
        string env,
        string defaultServiceName,
        IDataStreamsWriter? writer)
    {
        // We don't yet support primary tag in .NET yet
        _nodeHashBase = HashHelper.CalculateNodeHashBase(defaultServiceName, env, primaryTag: null);
        _isEnabled = writer is not null;
        _writer = writer;
    }

    public bool IsEnabled => Volatile.Read(ref _isEnabled);

    public static DataStreamsManager Create(
        ImmutableTracerSettings settings,
        IDiscoveryService discoveryService,
        string defaultServiceName)
    {
        var writer = settings.IsDataStreamsMonitoringEnabled
                         ? DataStreamsWriter.Create(settings, discoveryService, defaultServiceName)
                         : null;

        return new DataStreamsManager(settings.Environment, defaultServiceName, writer);
    }

    public async Task DisposeAsync()
    {
        Volatile.Write(ref _isEnabled, false);
        var writer = Interlocked.Exchange(ref _writer, null);

        if (writer is null)
        {
            return;
        }

        await writer.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Trys to extract a <see cref="PathwayContext"/>, from the provided <paramref name="headers"/>
    /// If data streams is disabled, or no pathway is present, returns null.
    /// </summary>
    public PathwayContext? ExtractPathwayContext<TCarrier>(TCarrier headers)
        where TCarrier : IBinaryHeadersCollection
        => IsEnabled ? DataStreamsContextPropagator.Instance.Extract(headers) : null;

    /// <summary>
    /// Injects a <see cref="PathwayContext"/> into headers
    /// </summary>
    /// <param name="context">The pathway context to inject</param>
    /// <param name="headers">The header collection to inject the headers into</param>
    public void InjectPathwayContext<TCarrier>(PathwayContext? context, TCarrier headers)
        where TCarrier : IBinaryHeadersCollection
    {
        if (!IsEnabled)
        {
            return;
        }

        if (context is not null)
        {
            DataStreamsContextPropagator.Instance.Inject(context.Value, headers);
            return;
        }

        // This shouldn't happen normally, as you should call SetCheckpoint before calling InjectPathwayContext
        // But if data streams was disabled, you call SetCheckpoint, and then data streams is enabled
        // you will hit this code path
        Log.Debug("Attempted to inject null pathway context");
    }

    /// <summary>
    /// Sets a checkpoint using the provided <see cref="PathwayContext"/>
    /// NOTE: <paramref name="edgeTags"/> must be in correct sort order
    /// </summary>
    /// <param name="parentPathway">The current pathway</param>
    /// <param name="edgeTags">Edge tags to set for the new pathway. MUST be sorted in alphabetical order</param>
    /// <returns>If disabled, returns <c>null</c>. Otherwise returns a new <see cref="PathwayContext"/></returns>
    public PathwayContext? SetCheckpoint(in PathwayContext? parentPathway, string[] edgeTags)
    {
        if (!IsEnabled)
        {
            return null;
        }

        try
        {
            var edgeStartNs = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();
            var pathwayStartNs = parentPathway?.PathwayStart ?? edgeStartNs;

            var nodeHash = HashHelper.CalculateNodeHash(_nodeHashBase, edgeTags);
            var parentHash = parentPathway?.Hash ?? default;
            var pathwayHash = HashHelper.CalculatePathwayHash(nodeHash, parentHash);

            var writer = Volatile.Read(ref _writer);
            writer?.Add(
                new StatsPoint(
                    edgeTags: edgeTags,
                    hash: pathwayHash,
                    parentHash: parentHash,
                    timestampNs: edgeStartNs,
                    pathwayLatencyNs: edgeStartNs - pathwayStartNs,
                    edgeLatencyNs: edgeStartNs - (parentPathway?.EdgeStart ?? edgeStartNs)));

            var pathway = new PathwayContext(
                hash: pathwayHash,
                pathwayStartNs: pathwayStartNs,
                edgeStartNs: edgeStartNs);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "SetCheckpoint with {PathwayHash}, {PathwayStart}, {EdgeStart}",
                    pathway.Hash,
                    pathway.PathwayStart,
                    pathway.EdgeStart);
            }

            return pathway;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting a data streams checkpoint. Disabling data streams monitoring");
            // Set this to false out of an abundance of caution.
            // We will look at being less conservative in the future
            // if we see intermittent errors for some reason.
            Volatile.Write(ref _isEnabled, false);
            return null;
        }
    }
}
