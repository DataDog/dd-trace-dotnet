// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    internal static class ContextPropagation
    {
        private const string KinesisKey = "_datadog";
        private const int MaxKinesisDataSize = 1024 * 1024; // 1MB
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

            if (request.Records[0].DuckCast<IContainsData>() is { } record)
            {
                InjectTraceIntoData(tracer, record, scope, streamName);
            }
        }

        public static void InjectTraceIntoData<TRecordRequest>(Tracer tracer, TRecordRequest record, Scope? scope, string? streamName)
            where TRecordRequest : IContainsData
        {
            if (scope is null)
            {
                return;
            }

            Dictionary<string, object>? jsonData = null;
            var context = new PropagationContext(scope.Span.Context, Baggage.Current);
            if (record.Data is not null)
            {
                jsonData = ParseDataObject(record.Data);
            }

            var propagatedContext = new Dictionary<string, object>();
            if (scope.Span is Span span && !string.IsNullOrEmpty(streamName))
            {
                var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
                if (dataStreamsManager != null && dataStreamsManager.IsEnabled)
                {
                    var payloadSize = jsonData?.Count > 0 && record.Data != null ? record.Data.Length : 0;
                    var edgeTags = new[] { "direction:out", $"topic:{streamName}", "type:kinesis" };
                    span.SetDataStreamsCheckpoint(
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
                return;
            }

            try
            {
                tracer.TracerManager.SpanContextPropagator.Inject(context, propagatedContext, default(DictionaryGetterAndSetter));
                jsonData[KinesisKey] = propagatedContext;

                var memoryStreamData = DictionaryToMemoryStream(jsonData);
                if (memoryStreamData.Length > MaxKinesisDataSize)
                {
                    return;
                }

                record.Data = memoryStreamData;
            }
            catch (Exception)
            {
                Log.Debug("Unable to inject trace context to Kinesis data.");
            }
        }

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

        public static Dictionary<string, object>? MemoryStreamToDictionary(MemoryStream stream)
        {
            // Convert the MemoryStream to a string
            var streamReader = new StreamReader(stream);
            var reader = new JsonTextReader(streamReader);
            var serializer = new JsonSerializer();

            // Deserialize the JSON string into a Dictionary<string, object>
            // JSON size doesn't matter because only a small piece is read
            // at a time from the stream.
            return serializer.Deserialize<Dictionary<string, object>>(reader);
        }

        public static MemoryStream DictionaryToMemoryStream(Dictionary<string, object> dictionary)
        {
            var memoryStream = new MemoryStream();
            var writer = new StreamWriter(memoryStream);
            var serializer = new JsonSerializer();
            serializer.Serialize(writer, dictionary);
            writer.Flush();

            // Reset the stream position before using it
            memoryStream.Position = 0;
            return memoryStream;
        }
    }
}
