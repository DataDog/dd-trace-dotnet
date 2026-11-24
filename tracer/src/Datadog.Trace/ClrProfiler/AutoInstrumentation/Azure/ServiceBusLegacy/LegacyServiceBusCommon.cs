// <copyright file="LegacyServiceBusCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Utils;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBusLegacy;

internal static class LegacyServiceBusCommon
{
    internal const string AssemblyName = "Microsoft.Azure.ServiceBus";
    internal const string MessageTypeName = "Microsoft.Azure.ServiceBus.Message";
    internal const string MinimumVersion = "1.0.0";
    internal const string MaximumVersion = "5.*.*";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LegacyServiceBusCommon));
    private static readonly EdgeTags DefaultProduceEdgeTags = new(["direction:out", "type:servicebus"]);
    private static readonly EdgeTags DefaultConsumeEdgeTags = new(["direction:in", "type:servicebus"]);
    private static readonly ConditionalWeakTable<object, PathwayHolder> PendingConsumePathways = new();

    internal static void OnDispatch<TMessage>(TMessage message)
        where TMessage : class
    {
        try
        {
            if (PendingConsumePathways.TryGetValue(message, out var holder))
            {
                PendingConsumePathways.Remove(message);
                DataStreamsManager.SetLastConsumePathway(holder.Pathway);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error restoring the DSM consume pathway of a Microsoft.Azure.ServiceBus message");
        }
    }

    internal static void OnProduce<TTarget>(TTarget instance, IEnumerable? messages)
        where TTarget : ILegacyClientEntity
    {
        if (messages is null || !TryGetDataStreamsManager(out var tracer, out var dataStreamsManager))
        {
            return;
        }

        try
        {
            var edgeTags = GetEdgeTags(dataStreamsManager, instance.Path, isConsume: false);
            var parent = tracer.InternalActiveScope?.Span is Span span ? span.Context.PathwayContext : null;

            foreach (var messageObj in messages)
            {
                if (messageObj?.DuckCast<ILegacyMessage>() is not { UserProperties: { } properties } message)
                {
                    continue;
                }

                var payloadSize = dataStreamsManager.IsInDefaultState ? 0 : GetMessageSize(message);
                var pathway = dataStreamsManager.SetCheckpoint(parent, CheckpointKind.Produce, edgeTags, payloadSize, 0);
                dataStreamsManager.InjectPathwayContextAsBase64String(pathway, new AzureHeadersCollectionAdapter(properties));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting a DSM produce checkpoint on a Microsoft.Azure.ServiceBus message");
        }
    }

    internal static void OnConsume<TTarget>(TTarget instance, IEnumerable? messages)
        where TTarget : ILegacyClientEntity
    {
        if (messages is null || !TryGetDataStreamsManager(out var tracer, out var dataStreamsManager))
        {
            return;
        }

        try
        {
            var edgeTags = GetEdgeTags(dataStreamsManager, instance.Path, isConsume: true);
            var span = tracer.InternalActiveScope?.Span as Span;

            foreach (var messageObj in messages)
            {
                if (messageObj?.DuckCast<ILegacyMessage>() is not { } message)
                {
                    continue;
                }

                PathwayContext? parent = null;
                if (message.UserProperties is { } properties)
                {
                    parent = dataStreamsManager.ExtractPathwayContextAsBase64String(new AzureHeadersCollectionAdapter(properties));
                }

                var timeInQueueMs = message.SystemProperties is { IsReceived: true } systemProperties
                                        ? (long)Math.Max(0, (DateTime.UtcNow - systemProperties.EnqueuedTimeUtc).TotalMilliseconds)
                                        : 0;
                var payloadSize = dataStreamsManager.IsInDefaultState ? 0 : GetMessageSize(message);

                PathwayContext? pathway;
                if (span is not null)
                {
                    span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Consume, edgeTags, payloadSize, timeInQueueMs, parent);
                    pathway = span.Context.PathwayContext;
                }
                else
                {
                    pathway = dataStreamsManager.SetCheckpoint(parent, CheckpointKind.Consume, edgeTags, payloadSize, timeInQueueMs);
                }

                if (pathway is { } consumed)
                {
                    PendingConsumePathways.Remove(messageObj);
                    PendingConsumePathways.Add(messageObj, new PathwayHolder(consumed));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting a DSM consume checkpoint on a Microsoft.Azure.ServiceBus message");
        }
    }

    private static bool TryGetDataStreamsManager(out Tracer tracer, out DataStreamsManager dataStreamsManager)
    {
        tracer = Tracer.Instance;
        dataStreamsManager = tracer.TracerManager.DataStreamsManager;
        return dataStreamsManager.IsEnabled
            && tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus);
    }

    private static EdgeTags GetEdgeTags(DataStreamsManager dataStreamsManager, string? entityPath, bool isConsume)
    {
        if (StringUtil.IsNullOrEmpty(entityPath))
        {
            return isConsume ? DefaultConsumeEdgeTags : DefaultProduceEdgeTags;
        }

        return isConsume
            ? dataStreamsManager.GetOrCreateEdgeTags(
                new ServiceBusEdgeTagCacheKey(entityPath!, IsConsume: true),
                static k => new EdgeTags(["direction:in", $"topic:{k.EntityPath}", "type:servicebus"]))
            : dataStreamsManager.GetOrCreateEdgeTags(
                new ServiceBusEdgeTagCacheKey(entityPath!, IsConsume: false),
                static k => new EdgeTags(["direction:out", $"topic:{k.EntityPath}", "type:servicebus"]));
    }

    private static long GetMessageSize(ILegacyMessage message)
    {
        long size = message.Body?.Length ?? 0;
        if (message.UserProperties is { } properties)
        {
            foreach (var pair in properties)
            {
                size += Encoding.UTF8.GetByteCount(pair.Key);
                size += MessageSizeHelper.TryGetSize(pair.Value);
            }
        }

        return size;
    }

    private sealed class PathwayHolder(PathwayContext pathway)
    {
        public PathwayContext Pathway { get; } = pathway;
    }
}
