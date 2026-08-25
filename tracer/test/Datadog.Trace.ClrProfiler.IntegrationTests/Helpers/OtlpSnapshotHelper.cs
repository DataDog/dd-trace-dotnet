// <copyright file="OtlpSnapshotHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.MockOtlp;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using VerifyTests;

// Aliased: unqualified "Span" would resolve to Datadog.Trace.Span, an ancestor-namespace member.
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    /// <summary>
    /// Shapes OTLP payloads into something a snapshot can be compared against: normalizing what
    /// changes per run, merging exports, and ordering spans and attributes. Normalization (below)
    /// still operates on the final JSON representation, since it has to write placeholder text into
    /// fields (e.g. trace/span IDs) whose real protobuf types can't hold arbitrary strings. Merging
    /// operates on the typed <c>MockOtlp</c> model instead, before that JSON is ever produced, since
    /// merging/sorting doesn't have that constraint and the typed model is easier to work with.
    /// </summary>
    internal static class OtlpSnapshotHelper
    {
        // Key-casing mappings come from OtlpFieldNames.FieldNamePairs, the single source of
        // truth shared with the structural JObject navigation in OpenTelemetrySdkTests -- this
        // covers every OTLP JSON key with a different casing per protocol, not just span fields
        // (e.g. it also includes the AnyValue oneof members like double_value/doubleValue). Enum
        // mappings are scrubber-only: OtlpFieldNames only tracks key casing, not the
        // int-vs-string-enum shape difference between the two OTLP renderings.
        private static readonly (string From, string To)[] ProtobufToJsonFieldNameMappings =
            OtlpFieldNames.FieldNamePairs
                          .Select(p => ($"\"{p.Protobuf}\"", $"\"{p.Json}\""))
                          .ToArray();

        private static readonly (string From, string To)[] ProtobufToJsonEnumMappings =
        {
            ("\"kind\": \"SPAN_KIND_INTERNAL\"", "\"kind\": 1"),
            ("\"kind\": \"SPAN_KIND_SERVER\"",   "\"kind\": 2"),
            ("\"kind\": \"SPAN_KIND_CLIENT\"",   "\"kind\": 3"),
            ("\"kind\": \"SPAN_KIND_PRODUCER\"", "\"kind\": 4"),
            ("\"kind\": \"SPAN_KIND_CONSUMER\"", "\"kind\": 5"),
            ("\"code\": \"STATUS_CODE_UNSET\"",  "\"code\": 0"),
            ("\"code\": \"STATUS_CODE_OK\"",     "\"code\": 1"),
            ("\"code\": \"STATUS_CODE_ERROR\"",  "\"code\": 2"),
        };

        private static readonly Regex TraceIdRegex = new(@"^([a-fA-F0-9]{32})$");

        private static readonly Regex SpanIdRegex = new(@"^([a-fA-F0-9]{16})$");

        private static readonly Regex CodeOriginFrameLineOrColumnKeyRegex = new(@"^_dd\.code_origin\.frames\.\d+\.(line|column)$", RegexOptions.Compiled);

        private static readonly Regex CodeOriginFrameFileKeyRegex = new(@"^_dd\.code_origin\.frames\.\d+\.file$", RegexOptions.Compiled);

        public static void AddProtobufToJsonScrubbers(VerifySettings settings)
        {
            foreach (var (from, to) in ProtobufToJsonFieldNameMappings)
            {
                settings.AddSimpleScrubber(from, to);
            }

            foreach (var (from, to) in ProtobufToJsonEnumMappings)
            {
                settings.AddSimpleScrubber(from, to);
            }
        }

        /// <summary>
        /// Replaces the resource attributes that change between runs or between machines.
        /// </summary>
        /// <param name="tracesRequests">The captured OTLP requests.</param>
        /// <param name="names">The field-name casing to use.</param>
        public static void NormalizeResourceAttributes(JToken tracesRequests, OtlpFieldNames names)
        {
            var stringValueKey = names.StringValue;

            foreach (var attribute in tracesRequests.SelectTokens("$..resource.attributes[?(@.key == 'telemetry.sdk.version')]"))
            {
                attribute["value"]![stringValueKey] = "sdk-version";
            }

            foreach (var attribute in tracesRequests.SelectTokens("$..resource.attributes[?(@.key == 'telemetry.sdk.name')]"))
            {
                attribute["value"]![stringValueKey] = "sdk-name";
            }

            foreach (var attribute in tracesRequests.SelectTokens("$..resource.attributes[?(@.key == 'git.commit.sha')]"))
            {
                attribute["value"]![stringValueKey] = "normalized-git-commit-sha";
            }
        }

        /// <summary>
        /// Asserts that the trace ids, span ids, and timestamps are well-formed, then replaces them
        /// with fixed placeholders so the payload is stable across runs.
        /// </summary>
        /// <param name="tracesRequests">The captured OTLP requests.</param>
        /// <param name="names">The field-name casing to use.</param>
        /// <param name="applicationStartTimeUnixNano">The time the sample application was started, used as a lower bound for span timestamps.</param>
        public static void NormalizeSpans(JToken tracesRequests, OtlpFieldNames names, long applicationStartTimeUnixNano)
        {
            var stringValueKey = names.StringValue;
            var traceIdKey = names.TraceId;
            var spanIdKey = names.SpanId;
            var parentSpanIdKey = names.ParentSpanId;
            var startTimeUnixNanoKey = names.StartTimeUnixNano;
            var endTimeUnixNanoKey = names.EndTimeUnixNano;
            var timeUnixNanoKey = names.TimeUnixNano;

            foreach (var span in tracesRequests.SelectTokens("$..spans[*]"))
            {
                // IDs are always base64 here: JsonFormatter renders bytes fields as base64 regardless
                // of wire encoding -- names.IsJson only selects field-name casing, not ID encoding.
                string traceIdData = ToTraceId(Convert.FromBase64String(span[traceIdKey]!.ToString()));
                string spanIdData = ToSpanId(Convert.FromBase64String(span[spanIdKey]!.ToString()));
                var spanStartTimeUnixNano = long.Parse(span[startTimeUnixNanoKey]!.ToString());
                var spanEndTimeUnixNano = long.Parse(span[endTimeUnixNanoKey]!.ToString());

                // Add strong assertions on unstable span information
                // spanStartTimeUnixNano.Should().BeGreaterThanOrEqualTo(applicationStartTimeUnixNano); // Remove one source of flakiness
                spanEndTimeUnixNano.Should().BeGreaterThanOrEqualTo(spanStartTimeUnixNano);
                traceIdData.Should().MatchRegex(TraceIdRegex);
                spanIdData.Should().MatchRegex(SpanIdRegex);
                if (span[parentSpanIdKey] is not null)
                {
                    string parentSpanIdData = ToSpanId(Convert.FromBase64String(span[parentSpanIdKey]!.ToString()));
                    parentSpanIdData.Should().MatchRegex(SpanIdRegex);
                }

                // Normalize the unstable span information for our snapshots
                span[startTimeUnixNanoKey] = "0";
                span[endTimeUnixNanoKey] = "0";
                span[traceIdKey] = "normalized-trace-id";
                span[spanIdKey] = "normalized-span-id";
                if (span[parentSpanIdKey] is not null)
                {
                    span[parentSpanIdKey] = "normalized-parent-span-id";
                }

                // Our JSON and Protobuf OTLP exporters differ in serialization behavior when there are no attributes.
                // Standardize them here by removing an empty array
                if (span["attributes"] is JArray attributes && attributes.Count == 0)
                {
                    ((JObject)span).Remove("attributes");
                }
            }

            foreach (var attribute in tracesRequests.SelectTokens("$..spans[*].attributes[?(@.key == 'otel.trace_id')]"))
            {
                attribute["value"]![stringValueKey] = "normalized-otel-trace-id";
            }

            // Samples bind a dynamic port, so server.port changes between runs. VerifyHelper's
            // SpanScrubbers handles this for the Datadog-format snapshots, but a text scrubber
            // can't reach the value through the OTLP key/value attribute shape.
            foreach (var attribute in tracesRequests.SelectTokens("$..spans[*].attributes[?(@.key == 'server.port')]"))
            {
                if (attribute["value"] is JObject value)
                {
                    // The port arrives as an int on client spans and as a double on server spans,
                    // so overwrite whichever value kind is present. Always writing a string keeps
                    // http/json and http/protobuf rendering identically, as the timestamps above do.
                    // TODO: Fix this up when implementing HTTP server spans as they should always
                    // be emitted as ints from our instrumentation once implemented.
                    foreach (var property in value.Properties())
                    {
                        property.Value = "00000";
                    }
                }
            }

            foreach (var link in tracesRequests.SelectTokens("$..links[*]"))
            {
                ToTraceId(Convert.FromBase64String(link[traceIdKey]!.ToString())).Should().MatchRegex(TraceIdRegex);
                ToSpanId(Convert.FromBase64String(link[spanIdKey]!.ToString())).Should().MatchRegex(SpanIdRegex);

                link[traceIdKey] = "normalized-trace-id";
                link[spanIdKey] = "normalized-span-id";
            }

            foreach (var @event in tracesRequests.SelectTokens("$..events[*]"))
            {
                ((JObject)@event).Remove(timeUnixNanoKey);
                ((JObject)@event).AddFirst(new JProperty(timeUnixNanoKey, "0"));
            }
        }

        /// <summary>
        /// Replaces the file/line/column of every <c>_dd.code_origin.frames.N.*</c> attribute with
        /// fixed placeholders, since the source line/column shift whenever the instrumented sample
        /// code changes, and the file path is checkout-dependent (absolute in CI, relative locally).
        /// </summary>
        /// <param name="tracesRequests">The captured OTLP requests.</param>
        public static void NormalizeCodeOriginAttributes(JToken tracesRequests)
        {
            foreach (var attribute in tracesRequests.SelectTokens("$..attributes[*]"))
            {
                var key = attribute["key"]?.ToString();
                if (key is null || attribute["value"] is not JObject value)
                {
                    continue;
                }

                if (CodeOriginFrameLineOrColumnKeyRegex.IsMatch(key))
                {
                    foreach (var property in value.Properties())
                    {
                        property.Value = "0";
                    }
                }
                else if (CodeOriginFrameFileKeyRegex.IsMatch(key))
                {
                    foreach (var property in value.Properties())
                    {
                        property.Value = NormalizeCodeOriginFilePath(property.Value!.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Sorts spans by name within each scope, leaving the request structure intact. Used when the
        /// payload comes from a real OTel SDK, which emits genuinely distinct instrumentation scopes.
        /// </summary>
        /// <param name="tracesRequests">The captured OTLP requests.</param>
        /// <param name="names">The field-name casing to use.</param>
        public static void SortSpansPerScope(JToken tracesRequests, OtlpFieldNames names)
        {
            foreach (var scopeSpan in tracesRequests.SelectTokens($"$..{names.ScopeSpans}[*]"))
            {
                if (scopeSpan["spans"] is JArray spansArray)
                {
                    var sorted = new JArray(spansArray.OrderBy(s => s["name"]?.ToString()));
                    scopeSpan["spans"] = sorted;
                }
            }
        }

        /// <summary>
        /// Sorts every span's attribute array by key. Attribute order otherwise follows tag
        /// enumeration order, which is not guaranteed to be stable across runtimes.
        /// </summary>
        /// <param name="tracesRequests">The captured OTLP requests.</param>
        public static void SortSpanAttributes(JToken tracesRequests)
        {
            foreach (var span in tracesRequests.SelectTokens("$..spans[*]"))
            {
                if (span["attributes"] is JArray attributes)
                {
                    ((JObject)span)["attributes"] = new JArray(
                        attributes.OrderBy(a => a["key"]?.ToString() ?? string.Empty, StringComparer.Ordinal));
                }
            }
        }

        /// <summary>
        /// Collapses every captured request into the first one. Asserts first that each request
        /// carries identical resource attributes and a single instrumentation scope, which holds for
        /// the Datadog SDK because it emits one application-level resource and does not yet track
        /// spans per instrumentation scope. Works on the underlying protobuf model directly, so
        /// callers merge and sort before ever converting to JSON, rather than after.
        /// </summary>
        /// <param name="requests">The trace requests to merge. Must be non-empty.</param>
        /// <param name="sortSpans">Orders the merged spans. Defaults to ordering by span name.</param>
        /// <returns>A clone of the first request's message, with every span merged into its resource/scope.</returns>
        public static ExportTraceServiceRequest MergeDatadogRequests(
            IReadOnlyList<MockOtlpTraceRequest> requests,
            Func<IEnumerable<OtlpSpan>, IEnumerable<OtlpSpan>>? sortSpans = null)
        {
            requests.Should().NotBeEmpty();

            // All requests must share one resource (DD_SERVICE, DD_VERSION, DD_ENV, etc.) -- true for
            // the DD SDK, not for OTel SDK apps, which run a second, distinct Traces SDK instance.
            Resource? previousResource = null;
            foreach (var request in requests)
            {
                request.Raw.ResourceSpans.Should().HaveCount(1);
                var resource = request.Raw.ResourceSpans[0].Resource;

                if (previousResource is null)
                {
                    previousResource = resource;
                }
                else
                {
                    resource.Equals(previousResource).Should().BeTrue();
                }
            }

            // The DD SDK doesn't yet track spans per instrumentation scope, so there's only one to merge.
            // TODO: Properly track spans per instrumentation scope.
            var allSpans = new List<OtlpSpan>();
            foreach (var request in requests)
            {
                request.Raw.ResourceSpans[0].ScopeSpans.Should().HaveCount(1);
                allSpans.AddRange(request.Raw.ResourceSpans[0].ScopeSpans[0].Spans);
            }

            // Not StringComparer.Ordinal: matches main's culture-aware OrderBy, since ordinal would
            // reorder names differing only by leading-letter case and break existing snapshots.
            sortSpans ??= spans => spans.OrderBy(s => s.Name);

            var merged = requests[0].Raw.Clone();
            var mergedSpans = merged.ResourceSpans[0].ScopeSpans[0].Spans;
            mergedSpans.Clear();
            mergedSpans.AddRange(sortSpans(allSpans));
            return merged;
        }

        /// <summary>
        /// Returns the string value of the first attribute matching any of <paramref name="keys"/>,
        /// or null when the span carries none of them. Accepts several keys because a tag's name
        /// changes with the semantic conventions in play, for example http.url versus url.full.
        /// </summary>
        /// <param name="span">The span to read from.</param>
        /// <param name="keys">The attribute keys to look for, in priority order.</param>
        /// <returns>The attribute value, or null when the span has none of the keys.</returns>
        public static string? GetAttributeStringValue(OtlpSpan span, params string[] keys)
        {
            foreach (var key in keys)
            {
                foreach (var attribute in span.Attributes)
                {
                    if (attribute.Key == key && attribute.Value.ValueCase == AnyValue.ValueOneofCase.StringValue)
                    {
                        return attribute.Value.StringValue;
                    }
                }
            }

            return null;
        }

        private static string ToHexString(byte[] bytes, int length)
        {
            bytes.Length.Should().Be(length);

            var traceId = new byte[length * 2];
            for (int i = 0; i < length; i++)
            {
                traceId[2 * i] = (byte)(bytes[i] >> 4);         // high 4 bits
                traceId[(2 * i) + 1] = (byte)(bytes[i] & 0x0F); // low 4 bits
            }

            // Convert each nibble (0-15) to its hex character
            var result = new char[length * 2];
            for (int i = 0; i < length * 2; i++)
            {
                result[i] = (char)(traceId[i] < 10 ? '0' + traceId[i] : 'a' + traceId[i] - 10);
            }

            return new string(result);
        }

        private static string ToTraceId(byte[] bytes) => ToHexString(bytes, 16);

        private static string ToSpanId(byte[] bytes) => ToHexString(bytes, 8);

        /// <summary>
        /// Trims a code-origin file path down to the repo-relative portion starting at "tracer",
        /// so absolute checkout paths (which differ between CI and local machines) don't break the
        /// snapshot. Mirrors VerifyHelper.NormalizeCodeOriginFilePaths, but keeps forward slashes to
        /// match how the rest of an OTLP payload's paths are rendered.
        /// </summary>
        private static string NormalizeCodeOriginFilePath(string path)
        {
            var normalized = path.Replace('\\', '/');
            var tracerIndex = normalized.IndexOf("tracer/", StringComparison.OrdinalIgnoreCase);
            return tracerIndex < 0 ? path : normalized.Substring(tracerIndex);
        }
    }
}
