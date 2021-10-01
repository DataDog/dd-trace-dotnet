// <copyright file="SendTracesToAgent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.HttpMessageHandlers;
using FluentAssertions;
using NUnit.Framework;

namespace Datadog.Trace.IntegrationTests
{
    public class SendTracesToAgent
    {
        private Tracer _tracer;
        private RecordHttpHandler _httpRecorder;

        [SetUp]
        public void Before()
        {
            var settings = new TracerSettings();

            var endpoint = new Uri("http://localhost:8126");
            _httpRecorder = new RecordHttpHandler();
            var api = new Api(endpoint, apiRequestFactory: null, statsd: null);
            var agentWriter = new AgentWriter(api, statsd: null);

            _tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null);
        }

        [Test]
        [Ignore("Run manually")]
        public async Task MinimalSpan()
        {
            var scope = _tracer.StartActive("Operation");
            scope.Dispose();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(1);
            _httpRecorder.Requests.Should().HaveCount(1);
            _httpRecorder.Responses.Should().HaveCount(1);
            _httpRecorder.Responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);

            var trace = _httpRecorder.Traces.Single();
            MsgPackHelpers.AssertSpanEqual(scope.Span, trace.Single());
        }

        [Test]
        [Ignore("Run manually")]
        public async Task CustomServiceName()
        {
            const string ServiceName = "MyService";

            var scope = _tracer.StartActive("Operation", serviceName: ServiceName);
            scope.Span.ResourceName = "This is a resource";
            scope.Dispose();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(1);
            _httpRecorder.Requests.Should().HaveCount(1);
            _httpRecorder.Responses.Should().HaveCount(1);
            _httpRecorder.Responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);

            var trace = _httpRecorder.Traces.Single();
            MsgPackHelpers.AssertSpanEqual(scope.Span, trace.Single());
        }

        [Test]
        [Ignore("Run manually")]
        public async Task Utf8Everywhere()
        {
            var scope = _tracer.StartActive("Aᛗᚪᚾᚾᚪ", serviceName: "На берегу пустынных волн");
            scope.Span.ResourceName = "η γλώσσα μου έδωσαν ελληνική";
            scope.Span.SetTag("யாமறிந்த", "ნუთუ კვლა");
            scope.Dispose();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(1);
            _httpRecorder.Requests.Should().HaveCount(1);
            _httpRecorder.Responses.Should().HaveCount(1);
            _httpRecorder.Responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);

            var trace = _httpRecorder.Traces.Single();
            MsgPackHelpers.AssertSpanEqual(scope.Span, trace.Single());
        }

        [Test]
        [Ignore("Run manually")]
        public async Task SubmitsOutOfOrderSpans()
        {
            var scope1 = _tracer.StartActive("op1");
            var scope2 = _tracer.StartActive("op2");
            scope1.Close();
            scope2.Close();

            await _httpRecorder.WaitForCompletion(1);
            _httpRecorder.Requests.Should().HaveCount(1);
            _httpRecorder.Responses.Should().HaveCount(1);
            _httpRecorder.Responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);

            var trace = _httpRecorder.Traces.Single();
            MsgPackHelpers.AssertSpanEqual(scope1.Span, trace[0].AsList()[0]);
            MsgPackHelpers.AssertSpanEqual(scope2.Span, trace[0].AsList()[1]);
        }

        [Test]
        [Ignore("Run manually")]
        public void WithDefaultFactory()
        {
            // This test does not check anything it validates that this codepath runs without exceptions
            var tracer = Tracer.Create();
            tracer.StartActive("Operation")
                  .Dispose();
        }

        [Test]
        [Ignore("Run manually")]
        public void WithGlobalTracer()
        {
            // This test does not check anything it validates that this codepath runs without exceptions
            Tracer.Instance.StartActive("Operation")
                  .Dispose();
        }
    }
}
