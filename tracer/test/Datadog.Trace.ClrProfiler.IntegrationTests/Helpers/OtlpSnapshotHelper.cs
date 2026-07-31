// <copyright file="OtlpSnapshotHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using VerifyTests;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    /// <summary>
    /// Shared plumbing for tests that snapshot OTLP payloads captured by the ddapm test agent.
    /// </summary>
    internal static class OtlpSnapshotHelper
    {
        // Single source of truth for translating an OTLP http/protobuf payload (rendered as JSON
        // by the test agent with snake_case field names and string-form enum values) to the
        // OTLP http/json shape (camelCase field names, integer enum values). When a new OTLP
        // field or enum reaches the serializer, add the mapping here.
        private static readonly (string From, string To)[] ProtobufToJsonFieldNameMappings =
        {
            ("\"resource_spans\"",        "\"resourceSpans\""),
            ("\"scope_spans\"",           "\"scopeSpans\""),
            ("\"trace_id\"",              "\"traceId\""),
            ("\"span_id\"",               "\"spanId\""),
            ("\"parent_span_id\"",        "\"parentSpanId\""),
            ("\"start_time_unix_nano\"",  "\"startTimeUnixNano\""),
            ("\"end_time_unix_nano\"",    "\"endTimeUnixNano\""),
            ("\"time_unix_nano\"",        "\"timeUnixNano\""),
            ("\"string_value\"",          "\"stringValue\""),
            ("\"double_value\"",          "\"doubleValue\""),
            ("\"int_value\"",             "\"intValue\""),
            ("\"bool_value\"",            "\"boolValue\""),
            ("\"array_value\"",           "\"arrayValue\""),
        };

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
        /// Clears the test-agent session, retrying if the agent is not yet ready.
        /// Ensures the OTLP HTTP endpoint is accepting connections before tests proceed.
        /// </summary>
        /// <param name="testAgentHost">The host the test agent is listening on.</param>
        /// <param name="maxRetries">The number of attempts to make before failing.</param>
        /// <param name="delayMs">The delay between attempts, in milliseconds.</param>
        /// <returns>A task that completes once the session has been cleared.</returns>
        public static async Task ClearTestAgentSessionAsync(string testAgentHost, int maxRetries = 5, int delayMs = 1000)
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"http://{testAgentHost}:4318/test/session/clear";

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    return;
                }
                catch (Exception) when (attempt < maxRetries)
                {
                    await Task.Delay(delayMs);
                }
            }

            // Final attempt -- let it throw if it fails
            var finalResponse = await httpClient.GetAsync(url);
            finalResponse.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Polls the test-agent for data until non-empty results are returned or timeout is reached.
        /// The sample app exports data during shutdown, so there can be a brief delay
        /// between process exit and data appearing in the test-agent. The timeout is generous
        /// because first-time gRPC connections (TCP+HTTP/2+TLS handshake) plus tracer shutdown
        /// flushing can stack up on slower CI runners.
        /// </summary>
        /// <param name="url">The test agent endpoint to poll.</param>
        /// <param name="timeoutSeconds">How long to keep polling before giving up.</param>
        /// <param name="pollIntervalMs">The delay between polls, in milliseconds.</param>
        /// <returns>The data returned by the test agent.</returns>
        public static async Task<JToken> WaitForTestAgentDataAsync(string url, int timeoutSeconds = 60, int pollIntervalMs = 500)
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var data = JToken.Parse(json);

                if (data.HasValues)
                {
                    return data;
                }

                await Task.Delay(pollIntervalMs);
            }

            // Final attempt -- return whatever we get so the caller's assertion shows the actual value
            var finalResponse = await httpClient.GetAsync(url);
            finalResponse.EnsureSuccessStatusCode();
            var finalJson = await finalResponse.Content.ReadAsStringAsync();
            return JToken.Parse(finalJson);
        }
    }
}
