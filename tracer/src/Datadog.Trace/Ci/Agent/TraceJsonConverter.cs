// <copyright file="TraceJsonConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Agent
{
    internal class TraceJsonConverter : JsonConverter<ArraySegment<Span>>
    {
        protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TraceJsonConverter>();
        private static readonly string ContainerId = ContainerMetadata.GetContainerId();
        private static readonly string TracerVersion = TracerConstants.AssemblyVersion;
        private static readonly string Language = ".NET";
        private static readonly string LanguageInterpreter = FrameworkDescription.Instance.Name;
        private static readonly string LanguageVersion = FrameworkDescription.Instance.ProductVersion;

        public override ArraySegment<Span> ReadJson(JsonReader reader, Type objectType, ArraySegment<Span> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, ArraySegment<Span> value, JsonSerializer serializer)
        {
            if (value.Count == 0)
            {
                return;
            }

            // Select the primary span
            Span selectedSpan = GetPrimarySpan(ref value);
            if (selectedSpan is null)
            {
                return;
            }

            WriteAgentPayload(writer, selectedSpan, ref value, serializer);
        }

        private void WriteAgentPayload(JsonWriter writer, Span selectedSpan, ref ArraySegment<Span> value, JsonSerializer serializer)
        {
            // Write agent Payload
            writer.WriteStartObject();

            writer.WritePropertyName("hostName");
            writer.WriteValue("none");

            writer.WritePropertyName("env");
            writer.WriteNull();

            writer.WritePropertyName("tracerPayloads");
            writer.WriteStartArray();
            // Write tracer payload
            WriteTracerPayload(writer, selectedSpan, ref value, serializer);
            writer.WriteEndArray();

            writer.WritePropertyName("tags");
            writer.WriteNull();

            writer.WritePropertyName("agentVersion");
            writer.WriteValue(0);

            writer.WritePropertyName("targetTPS");
            writer.WriteValue(0);

            writer.WritePropertyName("errorTPS");
            writer.WriteValue(0);

            writer.WriteEndObject();
        }

        private void WriteTracerPayload(JsonWriter writer, Span selectedSpan, ref ArraySegment<Span> value, JsonSerializer serializer)
        {
            // Write tracer payload
            writer.WriteStartObject();

            writer.WritePropertyName("container_id");
            writer.WriteValue(ContainerId);

            writer.WritePropertyName("language_name");
            writer.WriteValue(Language);

            writer.WritePropertyName("language_version");
            writer.WriteValue(LanguageVersion);

            writer.WritePropertyName("tracer_version");
            writer.WriteValue(TracerVersion);

            writer.WritePropertyName("runtime_id");
            writer.WriteValue(Tracer.RuntimeId);

            writer.WritePropertyName("chunks");
            writer.WriteStartArray();
            // Write trace chunk
            WriteTraceChunk(writer, selectedSpan, ref value, serializer);
            writer.WriteEndArray();

            writer.WritePropertyName("tags");
            writer.WriteNull();

            writer.WritePropertyName("env");
            writer.WriteValue(selectedSpan.GetTag(Trace.Tags.Env));

            writer.WritePropertyName("hostName");
            writer.WriteValue(HostMetadata.Instance.Hostname);

            writer.WritePropertyName("app_version");
            writer.WriteValue(selectedSpan.GetTag(Trace.Tags.Version));

            writer.WriteEndObject();
        }

        private void WriteTraceChunk(JsonWriter writer, Span selectedSpan, ref ArraySegment<Span> value, JsonSerializer serializer)
        {
            // Write trace chunk
            var priority = (int)(selectedSpan?.Context.TraceContext.SamplingPriority ?? SamplingPriority.AutoKeep);
            var origin = selectedSpan?.Context.Origin;

            writer.WriteStartObject();

            writer.WritePropertyName("priority");
            writer.WriteValue(priority);

            writer.WritePropertyName("origin");
            writer.WriteValue(origin);

            writer.WritePropertyName("spans");
            writer.WriteStartArray();
            for (var i = value.Offset; i < value.Count; i++)
            {
                if (value.Array[i] is not null)
                {
                    serializer.Serialize(writer, value.Array[i]);
                }
            }

            writer.WriteEndArray();

            writer.WritePropertyName("tags");
            writer.WriteNull();

            writer.WritePropertyName("dropped_trace");
            writer.WriteValue(false);

            writer.WriteEndObject();
        }

        private Span GetPrimarySpan(ref ArraySegment<Span> value)
        {
            Span selectedSpan = null;

            // We try to get the top level span
            for (var i = value.Offset; i < value.Count; i++)
            {
                if (value.Array[i] is not null && value.Array[i].IsTopLevel)
                {
                    selectedSpan = value.Array[i];
                    break;
                }
            }

            if (selectedSpan is null)
            {
                // If the top level span cannot be extracted, we try with the root span
                for (var i = value.Offset; i < value.Count; i++)
                {
                    if (value.Array[i] is not null && value.Array[i].IsRootSpan)
                    {
                        selectedSpan = value.Array[i];
                        break;
                    }
                }
            }

            if (selectedSpan is null)
            {
                // If not top level span neither root span, we select the first span on the array segment
                for (var i = value.Offset; i < value.Count; i++)
                {
                    if (value.Array[i] is not null)
                    {
                        selectedSpan = value.Array[i];
                        break;
                    }
                }
            }

            return selectedSpan;
        }
    }
}
