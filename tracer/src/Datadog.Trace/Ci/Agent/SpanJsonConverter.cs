// <copyright file="SpanJsonConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Agent
{
    internal class SpanJsonConverter : JsonConverter<Span>
    {
        protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SpanJsonConverter>();

        public override Span ReadJson(JsonReader reader, Type objectType, Span existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, Span value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("trace_id");
            writer.WriteValue(value.TraceId);

            writer.WritePropertyName("span_id");
            writer.WriteValue(value.SpanId);

            writer.WritePropertyName("name");
            writer.WriteValue(value.OperationName);

            writer.WritePropertyName("resource");
            writer.WriteValue(value.ResourceName);

            writer.WritePropertyName("service");
            writer.WriteValue(value.ServiceName);

            writer.WritePropertyName("type");
            writer.WriteValue(value.Type);

            writer.WritePropertyName("start");
            writer.WriteValue(value.StartTime.ToUnixTimeNanoseconds());

            writer.WritePropertyName("duration");
            writer.WriteValue(value.Duration.ToNanoseconds());

            if (value.Context.ParentId != null)
            {
                writer.WritePropertyName("parent_id");
                writer.WriteValue((ulong)value.Context.ParentId);
            }

            if (value.Error)
            {
                writer.WritePropertyName("error");
                writer.WriteValue(1);
            }

            if (value.Tags is TagsList tagList)
            {
                writer.WritePropertyName("meta");

                // Meta dictionary
                writer.WriteStartObject();
                bool isOriginWritten = false;
                foreach (var item in tagList.GetMetaKeyValues())
                {
                    if (item.Key == Trace.Tags.Origin)
                    {
                        isOriginWritten = true;
                    }

                    writer.WritePropertyName(item.Key);
                    writer.WriteValue(item.Value);
                }

                if (value.IsTopLevel)
                {
                    writer.WritePropertyName(Trace.Tags.RuntimeId);
                    writer.WriteValue(Tracer.RuntimeId);
                }

                string origin = value.Context.Origin;
                if (!isOriginWritten && !string.IsNullOrEmpty(origin))
                {
                    writer.WritePropertyName(Trace.Tags.Origin);
                    writer.WriteValue(origin);
                }

                writer.WriteEndObject();

                // Metrics dictionary
                writer.WritePropertyName("metrics");
                writer.WriteStartObject();
                foreach (var item in tagList.GetAllMetricsValues(value))
                {
                    writer.WritePropertyName(item.Key);
                    writer.WriteValue(item.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }
    }
}
