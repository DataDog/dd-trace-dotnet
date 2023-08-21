// <copyright file="DataStreamsManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Concurrent;
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
    private static readonly AsyncLocal<CheckpointInfo> PreviousCheckpoint = new();
    private readonly NodeHashBase _nodeHashBase;
    private bool _isEnabled;
    private IDataStreamsWriter? _writer;

    public DataStreamsManager(
        string? env,
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

        return new DataStreamsManager(settings.EnvironmentInternal, defaultServiceName, writer);
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
    /// <param name="checkpointKind">Is this a Produce or Consume operation?</param>
    /// <param name="edgeTags">Edge tags to set for the new pathway. MUST be sorted in alphabetical order</param>
    /// <param name="payloadSizeBytes">Payload size in bytes</param>
    /// <param name="timeInQueueMs">Edge start time extracted from the message metadata. Used only if this is start of the pathway</param>
    /// <returns>If disabled, returns <c>null</c>. Otherwise returns a new <see cref="PathwayContext"/></returns>
    public PathwayContext? SetCheckpoint(
        in PathwayContext? parentPathway,
        CheckpointKind checkpointKind,
        string[] edgeTags,
        long payloadSizeBytes,
        long timeInQueueMs)
    {
        if (!IsEnabled)
        {
            return null;
        }

        try
        {
            var previousContext = parentPathway;
            if (previousContext == null && PreviousCheckpoint.Value != null && PreviousCheckpoint.Value.Kind != checkpointKind)
            {
                previousContext = PreviousCheckpoint.Value.Context;
            }

            var nowNs = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();
            var edgeStartNs = previousContext == null && timeInQueueMs > 0 ? nowNs - (timeInQueueMs * 1000_000) : nowNs;
            var pathwayStartNs = previousContext?.PathwayStart ?? edgeStartNs;

            var nodeHash = HashHelper.CalculateNodeHash(_nodeHashBase, edgeTags);
            var parentHash = previousContext?.Hash ?? default;
            var pathwayHash = HashHelper.CalculatePathwayHash(nodeHash, parentHash);

            var writer = Volatile.Read(ref _writer);
            writer?.Add(
                new StatsPoint(
                    edgeTags: edgeTags,
                    hash: pathwayHash,
                    parentHash: parentHash,
                    timestampNs: nowNs,
                    pathwayLatencyNs: nowNs - pathwayStartNs,
                    edgeLatencyNs: nowNs - (previousContext?.EdgeStart ?? edgeStartNs),
                    payloadSizeBytes));

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

            // overwrite the previous checkpoint, so it can be used in the future if needed
            if (PreviousCheckpoint.Value == null || (PreviousCheckpoint.Value.Kind != checkpointKind))
            {
                PreviousCheckpoint.Value = new CheckpointInfo(pathway, checkpointKind);
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

    private class CheckpointInfo
    {
        public CheckpointInfo(PathwayContext context, CheckpointKind kind)
        {
            Context = context;
            Kind = kind;
        }

        public PathwayContext Context { get; }

        public CheckpointKind Kind { get; }
    }
}
