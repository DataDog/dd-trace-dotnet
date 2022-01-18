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

            writer.WritePropertyName("service");
            writer.WriteValue(value.ServiceName);

            writer.WritePropertyName("name");
            writer.WriteValue(value.OperationName);

            writer.WritePropertyName("resource");
            writer.WriteValue(value.ResourceName);

            writer.WritePropertyName("trace_id");
            writer.WriteValue(value.TraceId);

            writer.WritePropertyName("span_id");
            writer.WriteValue(value.SpanId);

            writer.WritePropertyName("parent_id");
            writer.WriteValue((ulong)(value.Context.ParentId ?? 0));

            writer.WritePropertyName("start");
            writer.WriteValue(value.StartTime.ToUnixTimeNanoseconds());

            writer.WritePropertyName("duration");
            writer.WriteValue(value.Duration.ToNanoseconds());

            writer.WritePropertyName("error");
            writer.WriteValue(value.Error ? 1 : 0);

            if (value.Tags is TagsList tagList)
            {
                writer.WritePropertyName("meta");

                // Meta dictionary
                writer.WriteStartObject();
                tagList.ForEachTag(item =>
                {
                    if (item.Key == Trace.Tags.Origin || item.Key == Trace.Tags.Env || item.Key == Trace.Tags.Version)
                    {
                        return;
                    }

                    writer.WritePropertyName(item.Key);
                    writer.WriteValue(item.Value);
                });

                writer.WriteEndObject();

                // Metrics dictionary
                writer.WritePropertyName("metrics");
                writer.WriteStartObject();

                tagList.ForEachMetric(item =>
                {
                    if (item.Key == Trace.Metrics.SamplingPriority)
                    {
                        return;
                    }

                    writer.WritePropertyName(item.Key);
                    writer.WriteValue(item.Value);
                });

                writer.WriteEndObject();
            }

            writer.WritePropertyName("type");
            writer.WriteValue(value.Type);

            writer.WriteEndObject();
        }
    }
}
