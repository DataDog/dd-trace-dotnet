// <copyright file="OpenTracingSendTracesToAgent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Net;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.HttpMessageHandlers;
using FluentAssertions;
using NUnit.Framework;

namespace Datadog.Trace.OpenTracing.IntegrationTests
{
    public class OpenTracingSendTracesToAgent
    {
        private OpenTracingTracer _tracer;
        private RecordHttpHandler _httpRecorder;

        [SetUp]
        public void Before()
        {
            var settings = new TracerSettings();

            var endpoint = new Uri("http://localhost:8126");
            _httpRecorder = new RecordHttpHandler();
            var api = new Api(endpoint, apiRequestFactory: null, statsd: null);
            var agentWriter = new AgentWriter(api, statsd: null);

            var tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null);
            _tracer = new OpenTracingTracer(tracer);
        }

        [Test]
        [Ignore("Run manually")]
        public async void MinimalSpan()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan("Operation")
                                               .Start();
            span.Finish();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(1);
            _httpRecorder.Requests.Should().HaveCount(1);
            _httpRecorder.Responses.Should().HaveCount(1);
            _httpRecorder.Responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);

            var trace = _httpRecorder.Traces.Single();
            MsgPackHelpers.AssertSpanEqual(span.DDSpan, trace.Single());
        }

        [Test]
        [Ignore("Run manually")]
        public async void CustomServiceName()
        {
            const string ServiceName = "MyService";

            var span = (OpenTracingSpan)_tracer.BuildSpan("Operation")
                                               .WithTag(DatadogTags.ResourceName, "This is a resource")
                                               .WithTag(DatadogTags.ServiceName, ServiceName)
                                               .Start();
            span.Finish();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(1);
            _httpRecorder.Requests.Should().HaveCount(1);
            _httpRecorder.Responses.Should().HaveCount(1);
            _httpRecorder.Responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);

            var trace = _httpRecorder.Traces.Single();
            MsgPackHelpers.AssertSpanEqual(span.DDSpan, trace.Single());
        }

        [Test]
        [Ignore("Run manually")]
        public async void Utf8Everywhere()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan("Aᛗᚪᚾᚾᚪ")
                                               .WithTag(DatadogTags.ResourceName, "η γλώσσα μου έδωσαν ελληνική")
                                               .WithTag(DatadogTags.ServiceName, "На берегу пустынных волн")
                                               .WithTag("யாமறிந்த", "ნუთუ კვლა")
                                               .Start();
            span.Finish();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(1);
            _httpRecorder.Requests.Should().HaveCount(1);
            _httpRecorder.Responses.Should().HaveCount(1);
            _httpRecorder.Responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);

            var trace = _httpRecorder.Traces.Single();
            MsgPackHelpers.AssertSpanEqual(span.DDSpan, trace.Single());
        }

        [Test]
        [Ignore("Run manually")]
        public void WithDefaultFactory()
        {
            // This test does not check anything it validates that this codepath runs without exceptions
            _tracer.BuildSpan("Operation")
                   .Start()
                   .Finish();
        }
    }
}
