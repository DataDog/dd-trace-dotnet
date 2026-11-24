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

    private const string DatadogPropertyName = "\"" + PayloadKey + "\"";

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
            var properties = ScanTopLevelProperties(messageText);
            if (properties is null)
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
            if (datadogContext.Count == 0)
            {
                return messageText;
            }

            var contextJson = SerializeDictionary(datadogContext);

            // Existing property values are spliced in verbatim: re-serializing them would round-trip
            // numeric tokens through double and silently alter high-precision application data.
            var builder = new StringBuilder(messageText.Length + contextJson.Length + PayloadKey.Length + 8 + properties.Count);
            builder.Append('{').Append('"').Append(PayloadKey).Append("\":").Append(contextJson);
            foreach (var property in properties)
            {
                builder.Append(',').Append(messageText, property.Start, property.End - property.Start);
            }

            return builder.Append('}').ToString();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unable to inject pathway context into an Azure Storage Queues message.");
            return messageText;
        }
    }

    internal static string? EncodeMessageTextWithPathwayContext(DataStreamsManager dataStreamsManager, string? encodedResult, bool isBase64, PathwayContext? pathwayContext)
    {
        if (encodedResult is null || pathwayContext is null)
        {
            return null;
        }

        try
        {
            var messageText = isBase64 ? Encoding.UTF8.GetString(Convert.FromBase64String(encodedResult)) : encodedResult;
            var injected = InjectPathwayContextIntoMessageText(dataStreamsManager, messageText, pathwayContext);
            if (injected is null || ReferenceEquals(injected, messageText))
            {
                return null;
            }

            var encoded = isBase64 ? Convert.ToBase64String(Encoding.UTF8.GetBytes(injected)) : injected;

            // Azure rejects the encoded message text, so the limit applies after Base64 expansion,
            // which grows the payload by roughly a third even when the decoded size fits.
            return Encoding.UTF8.GetByteCount(encoded) <= MaxMessageSizeBytes ? encoded : null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unable to inject pathway context into an Azure Storage Queues message body.");
            return null;
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

    private static List<PropertySpan>? ScanTopLevelProperties(string text)
    {
        var index = SkipWhitespace(text, 0);
        if (index >= text.Length || text[index] != '{')
        {
            return null;
        }

        var properties = new List<PropertySpan>();
        index++;

        while (true)
        {
            index = SkipWhitespace(text, index);
            if (index >= text.Length)
            {
                return null;
            }

            if (text[index] == '}')
            {
                index++;
                break;
            }

            var nameStart = index;
            if (!TrySkipString(text, ref index))
            {
                return null;
            }

            var isDatadogProperty = IsDatadogPropertyName(text, nameStart, index);

            index = SkipWhitespace(text, index);
            if (index >= text.Length || text[index] != ':')
            {
                return null;
            }

            index = SkipWhitespace(text, index + 1);
            if (!TrySkipValue(text, ref index))
            {
                return null;
            }

            if (!isDatadogProperty)
            {
                properties.Add(new PropertySpan(nameStart, index));
            }

            index = SkipWhitespace(text, index);
            if (index < text.Length && text[index] == ',')
            {
                index++;
                continue;
            }

            if (index < text.Length && text[index] == '}')
            {
                index++;
                break;
            }

            return null;
        }

        return SkipWhitespace(text, index) == text.Length ? properties : null;
    }

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsDatadogPropertyName(string text, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (text[i] == '\\')
            {
                try
                {
                    return JsonHelper.DeserializeObject<string>(text.Substring(start, end - start)) == PayloadKey;
                }
                catch
                {
                    return false;
                }
            }
        }

        var length = end - start;
        if (length != DatadogPropertyName.Length)
        {
            return false;
        }

        for (var i = 0; i < length; i++)
        {
            if (text[start + i] != DatadogPropertyName[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySkipValue(string text, ref int index)
    {
        if (index >= text.Length)
        {
            return false;
        }

        var character = text[index];
        if (character == '"')
        {
            return TrySkipString(text, ref index);
        }

        if (character == '{' || character == '[')
        {
            return TrySkipContainer(text, ref index);
        }

        var start = index;
        while (index < text.Length && !IsValueDelimiter(text[index]))
        {
            index++;
        }

        return index > start;
    }

    private static bool IsValueDelimiter(char character)
        => character is ',' or '}' or ']' or '/' || char.IsWhiteSpace(character);

    private static bool TrySkipContainer(string text, ref int index)
    {
        var depth = 0;
        while (index < text.Length)
        {
            var character = text[index];
            if (character == '"')
            {
                if (!TrySkipString(text, ref index))
                {
                    return false;
                }

                continue;
            }

            if (character == '/')
            {
                return false;
            }

            if (character == '{' || character == '[')
            {
                depth++;
            }
            else if (character == '}' || character == ']')
            {
                depth--;
                if (depth == 0)
                {
                    index++;
                    return true;
                }
            }

            index++;
        }

        return false;
    }

    private static bool TrySkipString(string text, ref int index)
    {
        if (index >= text.Length || text[index] != '"')
        {
            return false;
        }

        index++;
        while (index < text.Length)
        {
            var character = text[index];
            if (character == '\\')
            {
                index += 2;
                continue;
            }

            if (character == '"')
            {
                index++;
                return true;
            }

            index++;
        }

        return false;
    }

    private readonly struct PropertySpan
    {
        public PropertySpan(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; }

        public int End { get; }
    }
}
