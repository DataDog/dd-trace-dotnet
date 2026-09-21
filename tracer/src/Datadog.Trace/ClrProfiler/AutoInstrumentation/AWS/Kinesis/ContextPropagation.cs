// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    internal static class ContextPropagation
    {
        private const string KinesisKey = "_datadog";
        private const int MaxKinesisDataSize = 1024 * 1024; // 1MB
        private const int MaxKinesisPutRecordsRequestSize = 5 * 1024 * 1024; // 5MB total request size, including partition keys
        private const int MaxDsmHeaderSize = 34;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContextPropagation));

        public static void InjectTraceIntoRecords<TRecordsRequest>(Tracer tracer, TRecordsRequest request, Scope? scope, string? streamName)
            where TRecordsRequest : IContainsRecords
        {
            // request.Records is not null and has at least one element
            if (request.Records is not { Count: > 0 })
            {
                return;
            }

            var updatedRequestSize = 0L;
            var mutatedRecords = new List<PendingRecordUpdate>();
            foreach (var requestRecord in request.Records)
            {
                if (requestRecord?.DuckCast<IPutRecordsRequestEntry>() is not { } record)
                {
                    continue;
                }

                updatedRequestSize += GetPutRecordsRequestEntrySizeBytes(record);
                if (TryCreateInjectedData(tracer, record, scope, streamName, out var updatedData))
                {
                    updatedRequestSize += updatedData.Length - (record.Data?.Length ?? 0);
                    mutatedRecords.Add(new PendingRecordUpdate(record, updatedData));
                }
            }

            if (updatedRequestSize > MaxKinesisPutRecordsRequestSize)
            {
                Log.Debug("Kinesis PutRecords batch size too large to pass context");
                return;
            }

            foreach (var pendingUpdate in mutatedRecords)
            {
                pendingUpdate.Record.Data = pendingUpdate.UpdatedData;
            }
        }

        public static void InjectTraceIntoData<TRecordRequest>(Tracer tracer, TRecordRequest record, Scope? scope, string? streamName)
            where TRecordRequest : IContainsData
        {
            if (TryCreateInjectedData(tracer, record, scope, streamName, out var updatedData))
            {
                record.Data = updatedData;
            }
        }

        private static bool TryCreateInjectedData<TRecordRequest>(Tracer tracer, TRecordRequest record, Scope? scope, string? streamName, out MemoryStream updatedData)
            where TRecordRequest : IContainsData
        {
            updatedData = null!;
            if (scope is null)
            {
                return false;
            }

            Dictionary<string, object>? jsonData = null;
            if (record.Data is not null)
            {
                jsonData = ParseDataObject(record.Data);
            }

            var propagatedContext = new Dictionary<string, object>();
            if (scope.Span.Context != null && !StringUtil.IsNullOrEmpty(streamName))
            {
                var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
                if (dataStreamsManager is { IsEnabled: true })
                {
                    var payloadSize = jsonData?.Count > 0 && record.Data != null ? record.Data.Length : 0;
                    var edgeTags = dataStreamsManager.GetOrCreateEdgeTags(
                        new KinesisEdgeTagCacheKey(streamName, IsConsume: false),
                        static k => ["direction:out", $"topic:{k.StreamName}", "type:kinesis"]);
                    scope.Span.SetDataStreamsCheckpoint(
                        dataStreamsManager,
                        CheckpointKind.Produce,
                        edgeTags,
                        payloadSizeBytes: payloadSize,
                        timeInQueueMs: 0);

                    var adapter = new KinesisContextAdapter();
                    // We should not inject context if its size is comparable to the message size itself.
                    // This block doesn't modify the payload, the actual injection will happen later and only if the
                    // payload was parsed to json.
                    if (payloadSize != 0 && payloadSize > MaxDsmHeaderSize)
                    {
                        dataStreamsManager.InjectPathwayContext(scope.Span.Context.PathwayContext, adapter);
                    }

                    propagatedContext = adapter.GetDictionary();
                }
            }

            if (jsonData is null || jsonData.Count == 0)
            {
                return false;
            }

            if (scope.Span.Context is { } context)
            {
                try
                {
                    var propagationContext = new PropagationContext(context, Baggage.Current);
                    tracer.TracerManager.SpanContextPropagator.Inject(propagationContext, propagatedContext, default(DictionaryGetterAndSetter));
                    jsonData[KinesisKey] = propagatedContext;

                    var memoryStreamData = DictionaryToMemoryStream(jsonData);
                    if (memoryStreamData.Length > MaxKinesisDataSize)
                    {
                        return false;
                    }

                    updatedData = memoryStreamData;
                    return true;
                }
                catch (Exception)
                {
                    Log.Debug("Unable to inject trace context to Kinesis data.");
                }
            }

            return false;
        }

        private static long GetPutRecordsRequestEntrySizeBytes(IPutRecordsRequestEntry record)
        {
            return (record.Data?.Length ?? 0)
                 + GetUtf8Size(record.PartitionKey)
                 + GetUtf8Size(record.ExplicitHashKey);
        }

        private static int GetUtf8Size(string? value)
        {
            return value is null ? 0 : Encoding.UTF8.GetByteCount(value);
        }

        [TestingAndPrivateOnly]
        internal static Dictionary<string, object>? ParseDataObject(MemoryStream dataStream)
        {
            try
            {
                return MemoryStreamToDictionary(dataStream);
            }
            catch (Exception)
            {
                Log.Debug("Unable to parse Kinesis data. Trace context will not be injected.");
            }

            return null;
        }

        [TestingAndPrivateOnly]
        public static Dictionary<string, object>? MemoryStreamToDictionary(MemoryStream stream)
        {
            // Convert the MemoryStream to a string
            // Default values for StreamReader, but with leaveOpen:true
            using var streamReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,  bufferSize: 1024, leaveOpen: true);
            using var reader = new JsonTextReader(streamReader) { ArrayPool = JsonArrayPool.Shared };
            var serializer = new JsonSerializer();

            // Deserialize the JSON string into a Dictionary<string, object>
            // JSON size doesn't matter because only a small piece is read
            // at a time from the stream.
            return serializer.Deserialize<Dictionary<string, object>>(reader);
        }

        [TestingAndPrivateOnly]
        public static MemoryStream DictionaryToMemoryStream(Dictionary<string, object> dictionary)
        {
            var memoryStream = new MemoryStream();
            using var streamWriter = new StreamWriter(memoryStream, EncodingHelpers.Utf8NoBom, 1024, leaveOpen: true);
            using var writer = new JsonTextWriter(streamWriter) { ArrayPool = JsonArrayPool.Shared };
            var serializer = new JsonSerializer();
            serializer.Serialize(writer, dictionary);
            writer.Flush();

            // Reset the stream position before using it
            memoryStream.Position = 0;
            return memoryStream;
        }

        private struct PendingRecordUpdate
        {
            public PendingRecordUpdate(IPutRecordsRequestEntry record, MemoryStream updatedData)
            {
                Record = record;
                UpdatedData = updatedData;
            }

            public IPutRecordsRequestEntry Record { get; }

            public MemoryStream UpdatedData { get; }
        }
    }
}
