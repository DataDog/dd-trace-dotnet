// <copyright file="OtlpFieldNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    /// <summary>
    /// The test agent renders an OTLP http/json payload with camelCase field names and an
    /// http/protobuf payload with snake_case field names. This maps a protocol to the casing
    /// used when walking the rendered JSON.
    /// </summary>
    internal readonly struct OtlpFieldNames
    {
        private OtlpFieldNames(bool isJson)
        {
            IsJson = isJson;
        }

        public bool IsJson { get; }

        public string ResourceSpans => IsJson ? "resourceSpans" : "resource_spans";

        public string ScopeSpans => IsJson ? "scopeSpans" : "scope_spans";

        public string StringValue => IsJson ? "stringValue" : "string_value";

        public string IntValue => IsJson ? "intValue" : "int_value";

        public string TraceId => IsJson ? "traceId" : "trace_id";

        public string SpanId => IsJson ? "spanId" : "span_id";

        public string ParentSpanId => IsJson ? "parentSpanId" : "parent_span_id";

        public string StartTimeUnixNano => IsJson ? "startTimeUnixNano" : "start_time_unix_nano";

        public string EndTimeUnixNano => IsJson ? "endTimeUnixNano" : "end_time_unix_nano";

        public string TimeUnixNano => IsJson ? "timeUnixNano" : "time_unix_nano";

        public static OtlpFieldNames For(bool isJson) => new(isJson);
    }
}
