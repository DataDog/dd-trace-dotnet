// <copyright file="OtlpFieldNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    /// <summary>
    /// The test agent renders an OTLP http/json payload with camelCase key names and an
    /// http/protobuf payload with snake_case key names -- for every key in the payload, not just
    /// span fields (e.g. the AnyValue oneof members "double_value"/"doubleValue" appear inside
    /// span attributes, resource attributes, and log records alike). This maps a protocol to the
    /// casing used when walking the rendered JSON.
    /// </summary>
    internal readonly struct OtlpFieldNames
    {
        // Single source of truth for every OTLP JSON key whose casing differs between the
        // protobuf (snake_case) and json (camelCase) renderings. Also consumed by
        // OtlpSnapshotHelper to build its protobuf-to-json scrubbers. When a new OTLP key
        // reaches the serializer, add the mapping here and expose it as a property below.
        public static readonly (string Protobuf, string Json)[] FieldNamePairs =
        {
            ("resource_spans", "resourceSpans"),
            ("scope_spans", "scopeSpans"),
            ("trace_id", "traceId"),
            ("span_id", "spanId"),
            ("parent_span_id", "parentSpanId"),
            ("start_time_unix_nano", "startTimeUnixNano"),
            ("end_time_unix_nano", "endTimeUnixNano"),
            ("time_unix_nano", "timeUnixNano"),
            ("string_value", "stringValue"),
            ("int_value", "intValue"),
            ("double_value", "doubleValue"),
            ("bool_value", "boolValue"),
            ("array_value", "arrayValue"),
        };

        private static readonly Dictionary<string, string> ProtobufToJson =
            FieldNamePairs.ToDictionary(p => p.Protobuf, p => p.Json);

        private OtlpFieldNames(bool isJson)
        {
            IsJson = isJson;
        }

        public bool IsJson { get; }

        public string ResourceSpans => Get("resource_spans");

        public string ScopeSpans => Get("scope_spans");

        public string StringValue => Get("string_value");

        public string IntValue => Get("int_value");

        public string DoubleValue => Get("double_value");

        public string BoolValue => Get("bool_value");

        public string ArrayValue => Get("array_value");

        public string TraceId => Get("trace_id");

        public string SpanId => Get("span_id");

        public string ParentSpanId => Get("parent_span_id");

        public string StartTimeUnixNano => Get("start_time_unix_nano");

        public string EndTimeUnixNano => Get("end_time_unix_nano");

        public string TimeUnixNano => Get("time_unix_nano");

        public static OtlpFieldNames For(bool isJson) => new(isJson);

        private string Get(string protobufName) => IsJson ? ProtobufToJson[protobufName] : protobufName;
    }
}
