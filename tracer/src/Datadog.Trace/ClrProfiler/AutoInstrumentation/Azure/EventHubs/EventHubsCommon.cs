// <copyright file="EventHubsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Utils;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs;

internal static class EventHubsCommon
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventHubsCommon));

    // Used when the entity path is unknown — no topic tag
    private static readonly EdgeTags DefaultProduceEdgeTags = new(["direction:out", "type:eventhubs"]);
    private static readonly EdgeTags DefaultConsumeEdgeTags = new(["direction:in", "type:eventhubs"]);

    private static readonly ConditionalWeakTable<object, StrongBox<PathwayContext>> ConsumePathways = new();

    private static readonly ConditionalWeakTable<object, PendingBatchAdd> PendingBatchAddPathways = new();

    internal static CallTargetState CreateSenderSpan(
        IEventHubProducerClient instance,
        string operationName,
        IEnumerable? messages = null,
        int? messageCount = null,
        IEnumerable<SpanLink>? spanLinks = null)
    {
        var endpoint = instance.Connection?.ServiceEndpoint;
        var networkDestinationName = endpoint?.Host;
        var networkDestinationPort = endpoint?.Port is null or -1 or 5671 ?
                                        "5671" :
                                        endpoint.Port.ToStringInvariant();

        return CreateSenderSpanInternal(
            instance.EventHubName,
            networkDestinationName,
            networkDestinationPort,
            operationName,
            messages,
            messageCount,
            spanLinks);
    }

    internal static CallTargetState CreateSenderSpan(
        IEventDataBatch instance,
        string operationName,
        IEnumerable? messages = null,
        int? messageCount = null,
        IEnumerable<SpanLink>? spanLinks = null)
    {
        var networkDestinationName = instance.FullyQualifiedNamespace;

        return CreateSenderSpanInternal(
            instance.EventHubName,
            networkDestinationName,
            null,
            operationName,
            messages,
            messageCount,
            spanLinks);
    }

    private static CallTargetState CreateSenderSpanInternal(
        string? eventHubName,
        string? networkDestinationName,
        string? networkDestinationPort,
        string operationName,
        IEnumerable? messages,
        int? messageCount,
        IEnumerable<SpanLink>? spanLinks)
    {
        var tracer = Tracer.Instance;
        if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs))
        {
            return CallTargetState.GetDefault();
        }

        Scope? scope = null;

        try
        {
            var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAzureEventHubsTags(SpanKinds.Producer);
            tags.MessagingDestinationName = eventHubName;
            tags.MessagingOperation = operationName;

            var (serviceName, serviceNameSource) = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceNameMetadata(MessagingSchema.ServiceType.AzureEventHubs);
            scope = tracer.StartActiveInternal("azure_eventhubs." + operationName, tags: tags, serviceName: serviceName, serviceNameSource: serviceNameSource, links: spanLinks);
            var span = scope.Span;

            span.Type = SpanTypes.Queue;
            span.ResourceName = eventHubName;

            if (!string.IsNullOrEmpty(networkDestinationName))
            {
                tags.NetworkDestinationName = networkDestinationName;
            }

            if (!string.IsNullOrEmpty(networkDestinationPort))
            {
                tags.NetworkDestinationPort = networkDestinationPort;
            }

            var actualMessageCount = messageCount ?? (messages is ICollection collection ? collection.Count : 0);
            string? singleMessageId = null;

            if (actualMessageCount > 1)
            {
                tags.MessagingBatchMessageCount = actualMessageCount.ToStringInvariant();
            }

            if (actualMessageCount == 1 && messages != null)
            {
                foreach (var message in messages)
                {
                    if (message?.TryDuckCast<IEventData>(out var eventData) == true)
                    {
                        singleMessageId = eventData.MessageId;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(singleMessageId))
                {
                    tags.MessagingMessageId = singleMessageId;
                }
            }

            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AzureEventHubs);

            return new CallTargetState(scope);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating producer span");
            scope?.Dispose();
            return CallTargetState.GetDefault();
        }
    }

    internal static void SetProduceCheckpoints(Tracer tracer, IEnumerable? events, string? eventHubName)
    {
        // Only materialized collections are safe to enumerate: the sequence has not been
        // buffered by the SDK yet at this point, and iterating a lazy generator would consume it.
        if (events is not IReadOnlyCollection<object> eventCollection)
        {
            return;
        }

        var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
        if (dataStreamsManager is not { IsEnabled: true })
        {
            return;
        }

        var edgeTags = GetEdgeTags(dataStreamsManager, eventHubName, isConsume: false);

        foreach (var eventObj in eventCollection)
        {
            if (eventObj?.TryDuckCast<IEventData>(out var eventData) == true)
            {
                SetProduceCheckpoint(dataStreamsManager, tracer.InternalActiveScope?.Span, eventData, edgeTags);
            }
        }
    }

    internal static void SetBatchAddProduceCheckpoint(Tracer tracer, IEventData eventData, string? eventHubName)
    {
        var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
        if (dataStreamsManager is not { IsEnabled: true } || eventData.Instance is not { } instance)
        {
            return;
        }

        try
        {
            var span = tracer.InternalActiveScope?.Span;

            if (PendingBatchAddPathways.TryGetValue(instance, out var pending))
            {
                if (string.Equals(pending.EventHubName, eventHubName, StringComparison.Ordinal))
                {
                    if (span is not null)
                    {
                        span.Context.ManuallySetPathwayContextToPairMessages(pending.Pathway);
                        var hash = pending.Pathway.Hash.Value;
                        if (!dataStreamsManager.IsInDefaultState && hash != 0)
                        {
                            span.SetTag("pathway.hash", hash.ToString(CultureInfo.InvariantCulture));
                        }
                    }

                    if (eventData.Properties is { } properties)
                    {
                        dataStreamsManager.InjectPathwayContextAsBase64String(pending.Pathway, new AzureHeadersCollectionAdapter(properties));
                    }

                    return;
                }

                PendingBatchAddPathways.Remove(instance);
            }

            if (SetProduceCheckpoint(dataStreamsManager, span, eventData, GetEdgeTags(dataStreamsManager, eventHubName, isConsume: false)) is { } pathway)
            {
                PendingBatchAddPathways.Add(instance, new PendingBatchAdd(pathway, eventHubName));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting produce checkpoint for Event Hubs event added to a batch");
        }
    }

    internal static void CompleteBatchAdd(object eventData, bool accepted)
    {
        if (accepted)
        {
            PendingBatchAddPathways.Remove(eventData);
        }
    }

    internal static void SetConsumeCheckpoints(Tracer tracer, IReadOnlyList<object> events, string? eventHubName, Span? span)
    {
        var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
        if (dataStreamsManager is not { IsEnabled: true } || span is null)
        {
            return;
        }

        var edgeTags = GetEdgeTags(dataStreamsManager, eventHubName, isConsume: true);
        var consumeTime = span.StartTime.UtcDateTime;

        foreach (var eventObj in events)
        {
            try
            {
                if (eventObj?.TryDuckCast<IEventData>(out var eventData) == true)
                {
                    PathwayContext? pathwayContext = null;

                    if (eventData.Properties is { } properties)
                    {
                        pathwayContext = dataStreamsManager.ExtractPathwayContextAsBase64String(
                            new AzureHeadersCollectionAdapter(properties));
                    }

                    var produceTime = eventData.EnqueuedTime.UtcDateTime;
                    var queueTimeMs = Math.Max(0, (consumeTime - produceTime).TotalMilliseconds);
                    var msgSize = dataStreamsManager.IsInDefaultState ? 0 : GetEventSize(eventData);

                    span.SetDataStreamsCheckpoint(
                        dataStreamsManager,
                        CheckpointKind.Consume,
                        edgeTags,
                        msgSize,
                        (long)queueTimeMs,
                        pathwayContext);

                    if (span.Context.PathwayContext is { } consumePathway)
                    {
                        ConsumePathways.Remove(eventObj);
                        ConsumePathways.Add(eventObj, new StrongBox<PathwayContext>(consumePathway));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error setting consume checkpoint for Event Hubs event");
            }
        }
    }

    internal static void RestoreConsumePathway(object eventData)
    {
        if (ConsumePathways.TryGetValue(eventData, out var consumePathway))
        {
            DataStreamsManager.SetLastConsumePathway(consumePathway.Value);
        }
    }

    private static PathwayContext? SetProduceCheckpoint(DataStreamsManager dataStreamsManager, Span? span, IEventData eventData, EdgeTags edgeTags)
    {
        try
        {
            if (eventData.Properties is not { } properties)
            {
                return null;
            }

            var msgSize = dataStreamsManager.IsInDefaultState ? 0 : GetEventSize(eventData);

            // Batches are typically built without an active span around TryAdd, so the
            // checkpoint is set on the manager directly in that case, like a root produce.
            PathwayContext? pathway;
            if (span is not null)
            {
                span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, msgSize, 0);
                pathway = span.Context.PathwayContext;
            }
            else
            {
                pathway = dataStreamsManager.SetCheckpoint(parentPathway: null, CheckpointKind.Produce, edgeTags, msgSize, 0);
            }

            dataStreamsManager.InjectPathwayContextAsBase64String(
                pathway,
                new AzureHeadersCollectionAdapter(properties));

            return pathway;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting produce checkpoint for Event Hubs event");
            return null;
        }
    }

    private static EdgeTags GetEdgeTags(DataStreamsManager dataStreamsManager, string? entityPath, bool isConsume)
        => string.IsNullOrEmpty(entityPath)
               ? (isConsume ? DefaultConsumeEdgeTags : DefaultProduceEdgeTags)
               : dataStreamsManager.GetOrCreateEdgeTags(
                   new EventHubsEdgeTagCacheKey(entityPath!, isConsume),
                   static k => new EdgeTags([k.IsConsume ? "direction:in" : "direction:out", $"topic:{k.EntityPath}", "type:eventhubs"]));

    private static long GetEventSize(IEventData eventData)
    {
        long size = eventData.EventBody?.ToMemory().Length ?? 0;

        if (eventData.Properties is not { } properties)
        {
            return size;
        }

        foreach (var pair in properties)
        {
            size += Encoding.UTF8.GetByteCount(pair.Key);
            size += MessageSizeHelper.TryGetSize(pair.Value);
        }

        return size;
    }

    private sealed class PendingBatchAdd
    {
        public PendingBatchAdd(PathwayContext pathway, string? eventHubName)
        {
            Pathway = pathway;
            EventHubName = eventHubName;
        }

        public PathwayContext Pathway { get; }

        public string? EventHubName { get; }
    }
}
