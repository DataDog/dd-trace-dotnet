// <copyright file="AspNetCore5TraceTagging.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5TraceTagging : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        private const string RuleFile = "trace-tagging-rules.json";
        private const string Url = "/Health";

        public AspNetCore5TraceTagging(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: nameof(AspNetCore5TraceTagging), allowAutoRedirect: false)
        {
            Fixture = fixture;
            Fixture.SetOutput(outputHelper);
        }

        protected AspNetCoreTestFixture Fixture { get; }

        public override void Dispose()
        {
            base.Dispose();
            Fixture.SetOutput(null);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestTraceTaggingRules()
        {
            await Fixture.TryStartApp(this, enableSecurity: true, externalRulesFile: RuleFile);
            SetHttpPort(Fixture.HttpPort);

            var agent = Fixture.Agent;
            var spans = new List<MockSpan>();

            spans.Add(await SubmitTraceTaggingRequest(agent, "TraceTagging/v1"));
            spans.Add(await SubmitTraceTaggingRequest(agent, "TraceTagging/v2"));
            spans.Add(await SubmitTraceTaggingRequest(agent, "TraceTagging/v3"));
            spans.Add(await SubmitTraceTaggingRequest(agent, "TraceTagging/v4"));

            var settings = VerifyHelper.GetSpanVerifierSettings();
            await VerifySpans(spans.ToImmutableList(), settings);
        }

        private async Task<MockSpan> SubmitTraceTaggingRequest(MockTracerAgent agent, string userAgent)
        {
            ResetDefaultUserAgent();
            var minDateTime = DateTime.UtcNow;

            var (statusCode, _) = await SubmitRequest(Url, body: null, contentType: null, userAgent: userAgent);
            statusCode.Should().Be(HttpStatusCode.OK);

            var spans = await WaitForSpansAsync(agent, expectedSpans: 1, phase: userAgent, minDateTime, Url);
            return spans.Should().ContainSingle().Subject;
        }
    }
}
#endif
