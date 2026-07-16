// <copyright file="AspNetCoreNetFrameworkTopology.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
using VerifyTests;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    internal static class AspNetCoreNetFrameworkTopology
    {
        public const ulong IncomingTraceId = 123456789;
        public const ulong IncomingParentId = 987654321;
        public const string HeaderTags = "x-legacy-test-header:legacy.request.header,x-legacy-response-header:legacy.response.header,x-legacy-correlation-id";
        public const string PropagationStyleExtract = "Datadog,tracecontext,b3multi,baggage";

        private static readonly Regex SqlDatabaseRegex = new(@"(?<=db\.name: ).+(?=,\r?$)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex SqlHostRegex = new(@"(?<=out\.host: ).+(?=,\r?$)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex SqlPeerServiceRegex = new(@"(?<=peer\.service: ).+(?=,\r?$)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex SqlUserRegex = new(@"^[ \t]+db\.user: [^\r\n]+,\r?\n", RegexOptions.Compiled | RegexOptions.Multiline);

        public static Dictionary<string, string> CreateIncomingHeaders() =>
            new Dictionary<string, string>
            {
                ["x-datadog-trace-id"] = IncomingTraceId.ToString(),
                ["x-datadog-parent-id"] = IncomingParentId.ToString(),
                ["x-datadog-sampling-priority"] = "1",
            };

        public static IImmutableList<MockSpan> IncludeUpstreamSpan(IImmutableList<MockSpan> spans) =>
            ImmutableList.Create(
                              new MockSpan
                              {
                                  TraceId = IncomingTraceId,
                                  SpanId = IncomingParentId,
                                  Name = "upstream.request",
                                  Resource = "GET /baseline/sql",
                                  Service = "upstream-service",
                                  Type = "http",
                                  Tags = new Dictionary<string, string>
                                  {
                                      ["span.kind"] = "client",
                                      ["test.synthetic"] = "true",
                                  },
                                  Metrics = new Dictionary<string, double>(),
                              })
                         .AddRange(spans);

        public static IOrderedEnumerable<MockSpan> OrderSpans(IReadOnlyCollection<MockSpan> spans) =>
            spans.OrderBy(
                span => span.Name == "upstream.request"
                            ? 0
                            : span.Name == "aspnet_core.request"
                                ? 1
                                : 2);

        public static VerifySettings GetSpanVerifierSettings()
        {
            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(SqlDatabaseRegex, "sql-database");
            settings.AddRegexScrubber(SqlHostRegex, "sqlserver");
            settings.AddRegexScrubber(SqlPeerServiceRegex, "sqlserver");
            settings.AddRegexScrubber(SqlUserRegex, string.Empty);
            return settings;
        }
    }
}

#endif
