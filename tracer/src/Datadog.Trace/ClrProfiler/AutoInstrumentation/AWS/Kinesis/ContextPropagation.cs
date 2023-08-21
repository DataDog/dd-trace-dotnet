// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    internal static class ContextPropagation
    {
        private const string KinesisKey = "_datadog";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContextPropagation));

        public static void InjectTraceIntoRecords<TRecordsRequest>(IPutRecordsRequest request, SpanContext context)
        {
            // request.Records is not null and has at least one element
            if (request.Records is not { Count: > 0 })
            {
                return;
            }

            var record = request.Records[0].DuckCast<IContainsData>();
            InjectTraceIntoData<TRecordsRequest>(record, context);
        }

        public static void InjectTraceIntoData<TRecordRequest>(IContainsData record, SpanContext context)
        {
            if (record.Data is null)
            {
                return;
            }

            Inject<TRecordRequest>(record, context);
        }

        public static Dictionary<string, object> MemoryStreamToDictionary(MemoryStream stream)
        {
            // Convert the MemoryStream to a string
            var reader = new StreamReader(stream);

            // Deserialize the JSON string into a Dictionary<string, object>
            var dataDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(reader.ReadToEnd());

            return dataDict;
        }

        private static Dictionary<string, object> ParseDataObject(MemoryStream dataStream)
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

        private static void Inject<TRecord>(IContainsData record, SpanContext context)
        {
            var jsonData = ParseDataObject(record.Data);
            if (jsonData is null || jsonData.Count == 0)
            {
                return;
            }

            var propagatedContext = new Dictionary<string, string>();
            SpanContextPropagator.Instance.Inject(context, propagatedContext, default(DictionaryGetterAndSetter));
            jsonData[KinesisKey] = propagatedContext;

            try
            {
                // TODO: serializer to write bytes directly to new memory stream
                var jsonString = JsonConvert.SerializeObject(jsonData);
                var bytes = Encoding.UTF8.GetBytes(jsonString);
                record.Data = new MemoryStream(bytes);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
