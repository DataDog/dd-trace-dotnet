// <copyright file="MetricSeries.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Telemetry;

[JsonConverter(typeof(MetricSeriesJsonConverter))]
internal class MetricSeries : List<MetricDataPoint>
{
    public MetricSeries()
    {
    }

    public MetricSeries(List<MetricDataPoint> collection)
        : base(collection)
    {
    }

    public class MetricSeriesJsonConverter : JsonConverter<MetricSeries>
    {
        /// <summary>
        /// Used to serialize the data in the payload
        /// </summary>
        public override void WriteJson(JsonWriter writer, MetricSeries? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartArray();
            foreach (var point in value)
            {
                // Nested point array
                writer.WriteStartArray();
                writer.WriteValue(point.Timestamp);
                writer.WriteValue(point.Value);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }

        /// <summary>
        /// Only used for testing
        /// </summary>
        public override MetricSeries? ReadJson(JsonReader reader, Type objectType, MetricSeries? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var data = existingValue ?? new MetricSeries();

            var array = JArray.Load(reader);

            foreach (var token in array)
            {
                // don't care about perf here as this is only used in tests
                var pointArrayValues = token as JArray;
                if (pointArrayValues is null or not { Count: 2 })
                {
                    throw new InvalidOperationException($"Point array should contain two values, actually contained {pointArrayValues?.Count}");
                }

                var timestamp = pointArrayValues[0].Value<long>();
                var value = pointArrayValues[1].Value<int>();

                data.Add(new MetricDataPoint(timestamp, value));
            }

            return data;
        }
    }
}
