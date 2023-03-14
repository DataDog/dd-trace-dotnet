// <copyright file="AspNetCore5ExclusionFilters.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.RcmModels.Asm;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm
{
    public class Exclusions
    {
        internal const string ExampleExclusionFilters = """
[
  {
    "id": "865D405F-0CA7-4810-847D-EEE5107DD4E1",
    "conditions": [
      {
        "operator": "ip_match",
        "parameters": {
          "inputs": [
            {
              "address": "http.client_ip"
            }
          ],
          "list": [
            "192.0.240.56/28"
          ]
        }
      }
    ]
  },
  {
    "id": "0FD9CE7B-3C2C-4EDE-9AEA-B0A5B5F18014",
    "conditions": [
      {
        "operator": "match_regex",
        "parameters": {
          "inputs": [
            {
              "address": "server.request.headers.no_cookies",
              "key_path": [
                "user-agent"
              ]
            }
          ],
          "regex": "MyAllowedScanner"
        }
      }
    ],
    "rules_target": [
      {
        "tags": {
          "category": "attack_attempt",
          "type": "security_scanner"
        }
      },
      {
        "rule_id": "sqr-000-011"
      }
    ]
  }
]
""";
    }

    public class AspNetCore5ExclusionFiltersAllowIp : RcmBase
    {
        public AspNetCore5ExclusionFiltersAllowIp(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: nameof(AspNetCore5ExclusionFiltersAllowIp))
        {
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task RunTest()
        {
            var scrubbers = VerifyHelper.SpanScrubbers.Where(s => s.RegexPattern.ToString() != @"http.client_ip: (.)*(?=,)");
            var settings = VerifyHelper.GetSpanVerifierSettings(scrubbers: scrubbers, parameters: new object[] {  });

            var url = "/admin/?[$slice]=value";
            await TryStartApp();
            var agent = Fixture.Agent;

            var spanBeforeAsmData = await SendRequestsAsync(agent, url, null, 1, 1, string.Empty, ip: "192.0.240.56");

            using var reader = new StringReader(Exclusions.ExampleExclusionFilters);
            using var jsonReader = new JsonTextReader(reader);
            var root = (JArray)JToken.ReadFrom(jsonReader);

            var acknowledgeId = nameof(AspNetCore5ExclusionFiltersAllowIp) + Guid.NewGuid();

            var product = new AsmProduct();
            await agent.SetupRcmAndWait(
                Output,
                new[] { ((object)new Payload { Exclusions = root }, acknowledgeId) },
                product.Name,
                appliedServiceNames: new[] { acknowledgeId });

            var spanAfterAsmData = await SendRequestsAsync(agent, url, null, 1, 1, string.Empty, ip: "192.0.240.56");
            var spans = new List<MockSpan>();
            spans.AddRange(spanBeforeAsmData);
            spans.AddRange(spanAfterAsmData);
            await VerifySpans(spans.ToImmutableList(), settings);
        }
    }

    public class AspNetCore5ExclusionFiltersAllowUserAgent : RcmBase
    {
        public AspNetCore5ExclusionFiltersAllowUserAgent(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: nameof(AspNetCore5ExclusionFiltersAllowUserAgent))
        {
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task RunTest()
        {
            await TryStartApp();
            var agent = Fixture.Agent;

            var url = "/health/params/appscan_fingerprint";

            var spanBeforeAsmData = await SendRequestsAsync(agent, url, null, 1, 1, string.Empty, userAgent: "MyAllowedScanner");

            using var reader = new StringReader(Exclusions.ExampleExclusionFilters);
            using var jsonReader = new JsonTextReader(reader);
            var root = (JArray)JToken.ReadFrom(jsonReader);

            var acknowledgeId = nameof(AspNetCore5ExclusionFiltersAllowUserAgent) + Guid.NewGuid();

            var product = new AsmProduct();
            await agent.SetupRcmAndWait(
                Output,
                new[]
                {
                    (
                        (object)new Payload { Exclusions = root }, acknowledgeId),
                },
                product.Name,
                appliedServiceNames: new[] { acknowledgeId });

            var settings = VerifyHelper.GetSpanVerifierSettings();

            var spanAfterAsmData = await SendRequestsAsync(agent, url, null, 1, 1, string.Empty, userAgent: "MyAllowedScanner");
            var spans = new List<MockSpan>();
            spans.AddRange(spanBeforeAsmData);
            spans.AddRange(spanAfterAsmData);
            await VerifySpans(spans.ToImmutableList(), settings);
        }
    }
}
#endif
