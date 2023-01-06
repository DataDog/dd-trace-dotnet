// <copyright file="AspNetCore5ExclusionFilters.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

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
    public class AspNetCore5ExclusionFilters : AspNetBase, IDisposable
    {
// TODO we used to have a second condition in the first filter, but that seems to break it ... ask WAF people why
// @",
// {
// ""operator"": ""match_regex"",
// ""parameters"": {
//   ""inputs"": [
//  {
//    ""address"": ""server.request.uri.raw""
//  }
//   ],
//   ""regex"": ""^/admin""
// }
// }"

        private const string Exclusions = @"[
  {
    ""id"": ""865D405F-0CA7-4810-847D-EEE5107DD4E1"",
    ""conditions"": [
      {
        ""operator"": ""ip_match"",
        ""parameters"": {
          ""inputs"": [
            {
              ""address"": ""http.client_ip""
            }
          ],
          ""list"": [
            ""192.0.240.56/28""
          ]
        }
      }
    ]
  },
  {
    ""id"": ""0FD9CE7B-3C2C-4EDE-9AEA-B0A5B5F18014"",
    ""conditions"": [
      {
        ""operator"": ""match_regex"",
        ""parameters"": {
          ""inputs"": [
            {
              ""address"": ""server.request.headers.no_cookies"",
              ""key_path"": [
                ""user-agent""
              ]
            }
          ],
          ""regex"": ""MyAllowedScanner""
        }
      }
    ],
    ""rules_target"": [
      {
        ""tags"": {
          ""category"": ""attack_attempt"",
          ""type"": ""security_scanner""
        }
      },
      {
        ""rule_id"": ""sqr-000-011""
      }
    ]
  }
]";

        public AspNetCore5ExclusionFilters(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: nameof(AspNetCore5ExclusionFilters))
        {
        }

        [SkippableTheory]
        [InlineData("allow-ip-url-combo")]
        [Trait("RunOnWindows", "True")]
        public async Task TestAllowIp(string test)
        {
            var scrubbers = VerifyHelper.SpanScrubbers.Where(s => s.RegexPattern.ToString() != @"http.client_ip: (.)*(?=,)");
            var settings = VerifyHelper.GetSpanVerifierSettings(scrubbers: scrubbers, parameters: new object[] { test });

            var url = "/admin/?[$slice]=value";
            var agent = await RunOnSelfHosted(true);

            var spanBeforeAsmData = await SendRequestsAsync(agent, url, null, 1, 1, string.Empty, ip: "192.0.240.56");

            using var reader = new StringReader(Exclusions);
            using var jsonReader = new JsonTextReader(reader);
            var root = (JArray)JToken.ReadFrom(jsonReader);

            var product = new AsmProduct();
            agent.SetupRcm(
                Output,
                new[]
                {
                    (
                        (object)new Payload { Exclusions = root }, "asm_exclusions"),
                },
                product.Name);
            var request1 = await agent.WaitRcmRequestAndReturnLast();
            // TODO add log and wait for this log
            await Task.Delay(1500);

            var spanAfterAsmData = await SendRequestsAsync(agent, url, null, 1, 1, string.Empty, ip: "192.0.240.56");
            var spans = new List<MockSpan>();
            spans.AddRange(spanBeforeAsmData);
            spans.AddRange(spanAfterAsmData);
            await VerifySpans(spans.ToImmutableList(), settings, true);
        }

        [SkippableTheory]
        [InlineData("allow-user-agent", "/Health/?arg&[$slice]")]
        [InlineData("allow-user-agent", "/health/params/appscan_fingerprint")]
        [Trait("RunOnWindows", "True")]
        public async Task TestAllowUserAgent(string test, string url)
        {
            var agent = await RunOnSelfHosted(true);

            var spanBeforeAsmData = await SendRequestsAsync(agent, url, null, 1, 1, string.Empty, userAgent: "MyAllowedScanner");

            using var reader = new StringReader(Exclusions);
            using var jsonReader = new JsonTextReader(reader);
            var root = (JArray)JToken.ReadFrom(jsonReader);

            var product = new AsmProduct();
            agent.SetupRcm(
                Output,
                new[]
                {
                    (
                        (object)new Payload { Exclusions = root }, "asm_exclusions"),
                },
                product.Name);
            var request1 = await agent.WaitRcmRequestAndReturnLast();
            await Task.Delay(1500);

            var settings = VerifyHelper.GetSpanVerifierSettings(test, url);

            var spanAfterAsmData = await SendRequestsAsync(agent, url, null, 1, 1, string.Empty, userAgent: "MyAllowedScanner");
            var spans = new List<MockSpan>();
            spans.AddRange(spanBeforeAsmData);
            spans.AddRange(spanAfterAsmData);
            await VerifySpans(spans.ToImmutableList(), settings, true);
        }
    }
}
#endif
