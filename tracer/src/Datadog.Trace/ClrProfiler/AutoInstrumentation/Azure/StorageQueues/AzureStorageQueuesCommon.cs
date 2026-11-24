// <copyright file="AzureStorageQueuesCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.StorageQueues;

internal static class AzureStorageQueuesCommon
{
    internal const string IntegrationName = nameof(IntegrationId.AzureStorageQueues);
    internal const IntegrationId IntegrationId = Configuration.IntegrationId.AzureStorageQueues;

    internal const string QueueTypeTag = "azure-storage-queue";

    internal const string PayloadKey = "_datadog";

    internal const int MaxMessageSizeBytes = 64 * 1024;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AzureStorageQueuesCommon));

    private static readonly EdgeTags DefaultProduceEdgeTags = new(["direction:out", $"type:{QueueTypeTag}"]);
    private static readonly EdgeTags DefaultConsumeEdgeTags = new(["direction:in", $"type:{QueueTypeTag}"]);

    // Azure Storage Queue messages carry no headers, so the pathway context travels inside
    // JSON-object payloads. The send integration records the produce checkpoint and publishes
    // the pathway here; the payload rewrite happens later, inside QueueMessageCodec, after
    // the SDK may have awaited (e.g. for client-side encryption), so the handoff must flow across awaits.
    private static readonly AsyncLocal<PathwayContext?> PendingPathwayContext = new();

    internal static PathwayContext? CurrentPendingPathwayContext => PendingPathwayContext.Value;

    internal static void SetPendingPathwayContext(PathwayContext? context) => PendingPathwayContext.Value = context;

    internal static EdgeTags GetEdgeTags(DataStreamsManager dataStreamsManager, string? queueName, bool isConsume)
    {
        if (string.IsNullOrEmpty(queueName))
        {
            return isConsume ? DefaultConsumeEdgeTags : DefaultProduceEdgeTags;
        }

        return dataStreamsManager.GetOrCreateEdgeTags(
            new AzureStorageQueuesEdgeTagCacheKey(queueName!, isConsume),
            static k => new EdgeTags(
            [
                $"direction:{(k.IsConsume ? "in" : "out")}",
                $"topic:{k.QueueName}",
                $"type:{QueueTypeTag}",
            ]));
    }

    internal static CallTargetState CreateScope(Tracer tracer, string spanKind, string? queueName)
    {
        Scope? scope = null;

        try
        {
            var perTraceSettings = tracer.CurrentTraceSettings;
            if (!perTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return CallTargetState.GetDefault();
            }

            var operationName = spanKind == SpanKinds.Producer
                ? perTraceSettings.Schema.Messaging.GetOutboundOperationName(MessagingSchema.OperationType.AzureStorageQueues)
                : perTraceSettings.Schema.Messaging.GetInboundOperationName(MessagingSchema.OperationType.AzureStorageQueues);

            var tags = perTraceSettings.Schema.Messaging.CreateAzureStorageQueuesTags(spanKind);
            tags.MessagingSystem = "azurestoragequeues";
            tags.MessagingOperation = spanKind == SpanKinds.Producer ? "send" : "receive";
            tags.MessagingDestinationName = queueName;

            var (serviceName, serviceNameSource) = perTraceSettings.Schema.Messaging.GetServiceNameMetadata(MessagingSchema.ServiceType.AzureStorageQueues);
            scope = tracer.StartActiveInternal(operationName, tags: tags, serviceName: serviceName, serviceNameSource: serviceNameSource);
            var span = scope.Span;

            span.Type = SpanTypes.Queue;
            span.ResourceName = queueName ?? operationName;

            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);

            return new CallTargetState(scope);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating Azure Storage Queues scope.");
            scope?.Dispose();
            return CallTargetState.GetDefault();
        }
    }

    internal static string? InjectPathwayContextIntoMessageText(DataStreamsManager dataStreamsManager, string? messageText, PathwayContext? pathwayContext)
    {
        if (messageText is null || pathwayContext is null)
        {
            return messageText;
        }

        try
        {
            if (!messageText.StartsWith('{'))
            {
                return messageText;
            }

            var payload = ParseJsonDictionary(messageText);
            if (payload is null || payload.Count == 0)
            {
                return messageText;
            }

            var datadogContext = new Dictionary<string, object>();
            dataStreamsManager.InjectPathwayContextAsBase64String(pathwayContext, new AzureHeadersCollectionAdapter(datadogContext));

            payload[PayloadKey] = datadogContext;

            var injected = SerializeDictionary(payload);
            if (Encoding.UTF8.GetByteCount(injected) > MaxMessageSizeBytes)
            {
                return messageText;
            }

            return injected;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unable to inject pathway context into an Azure Storage Queues message.");
            return messageText;
        }
    }

    internal static PathwayContext? ExtractPathwayContextFromMessageText(DataStreamsManager dataStreamsManager, string? messageText)
    {
        if (string.IsNullOrEmpty(messageText))
        {
            return null;
        }

        try
        {
            if (!messageText!.StartsWith('{'))
            {
                return null;
            }

            var payload = ParseJsonDictionary(messageText!);
            if (payload is null
                || !payload.TryGetValue(PayloadKey, out var datadog)
                || datadog is not JObject datadogObject)
            {
                return null;
            }

            var headers = new Dictionary<string, object>(datadogObject.Count);
            foreach (var property in datadogObject.Properties())
            {
                if (property.Value is not { } token)
                {
                    continue;
                }

                headers[property.Name] = token.Type == JTokenType.String
                    ? token.ToString()
                    : JsonHelper.TokenToString(token, Formatting.None);
            }

            return dataStreamsManager.ExtractPathwayContextAsBase64String(new AzureHeadersCollectionAdapter(headers));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unable to extract pathway context from an Azure Storage Queues message.");
            return null;
        }
    }

    internal static Dictionary<string, object>? ParseJsonDictionary(string messageText)
    {
        try
        {
            using var stringReader = new StringReader(messageText);
            using var reader = new JsonTextReader(stringReader)
            {
                ArrayPool = JsonArrayPool.Shared,
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal,
            };
            var serializer = new JsonSerializer();
            return serializer.Deserialize<Dictionary<string, object>>(reader);
        }
        catch (Exception)
        {
            Log.Debug("Unable to parse an Azure Storage Queues message body as a JSON object.");
            return null;
        }
    }

    private static string SerializeDictionary(Dictionary<string, object> dictionary)
    {
        var stringBuilder = new StringBuilder();
        using var stringWriter = new StringWriter(stringBuilder);
        using var writer = new JsonTextWriter(stringWriter) { ArrayPool = JsonArrayPool.Shared };
        var serializer = new JsonSerializer();
        serializer.Serialize(writer, dictionary);
        writer.Flush();
        return stringBuilder.ToString();
    }
}
