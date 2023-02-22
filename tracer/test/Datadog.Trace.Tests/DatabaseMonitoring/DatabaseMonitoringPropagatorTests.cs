// <copyright file="DatabaseMonitoringPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DatabaseMonitoring;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.DatabaseMonitoring
{
    public class DatabaseMonitoringPropagatorTests
    {
        private readonly Tracer _tracer;
        private readonly Mock<IAgentWriter> _writerMock;

        public DatabaseMonitoringPropagatorTests(ITestOutputHelper output)
        {
            var settings = new TracerSettings();
            _writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();

            _tracer = new Tracer(settings, _writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
        }

        [Theory]
        [InlineData("100", SamplingPriorityValues.UserKeep, "")]
        [InlineData("string100", SamplingPriorityValues.UserKeep, "")]
        [InlineData("disabled", SamplingPriorityValues.UserKeep, "")]
        [InlineData("Service", SamplingPriorityValues.AutoReject, "/*ddps='Test.Service',dddbs='Test.Service-mysql'*/")]
        [InlineData("fUll", SamplingPriorityValues.AutoKeep, "/*ddps='Test.Service',dddbs='Test.Service-mysql',traceparent='00-00000000000000006172c1c9a829c71c-05a5f7b5320d6e4d-01'*/")]
        [InlineData("full", SamplingPriorityValues.UserReject, "/*ddps='Test.Service',dddbs='Test.Service-mysql',traceparent='00-00000000000000006172c1c9a829c71c-05a5f7b5320d6e4d-00'*/")]
        public void ExpectedCommentInjected(string propagationMode, int? samplingPriority, string expectedComment)
        {
            DbmPropagationLevel dbmPropagationLevel;
            Enum.TryParse(propagationMode, true, out dbmPropagationLevel);

            var context = new SpanContext(traceId: 7021887840877922076, spanId: 407003698947780173, samplingPriority: samplingPriority, serviceName: "Test.Service-mysql", "origin");

            context.SpanId.Should().Be(407003698947780173);

            var returnedComment = DatabaseMonitoringPropagator.PropagateSpanData(dbmPropagationLevel, "Test.Service", context);

            returnedComment.Should().Be(expectedComment);
        }

        [Theory]

        [InlineData("/*ddps='Test.Service',dddbs='Test.Service-mysql',ddpv='1.0.0',dde='testing'*/", "testing", "1.0.0")]
        [InlineData("/*ddps='Test.Service',dddbs='Test.Service-mysql',ddpv='1.0.0'*/", null, "1.0.0")]
        [InlineData("/*ddps='Test.Service',dddbs='Test.Service-mysql'*/", null, null)]
        [InlineData("/*ddps='Test.Service',dddbs='Test.Service-mysql',dde='testing'*/", "testing", null)]
        public void ExpectedTagsInjected(string expectedComment, string env = null, string version = null)
        {
            var span = _tracer.StartSpan(operationName: "mysql.query", parent: SpanContext.None, serviceName: "Test.Service-mysql", traceId: 7021887840877922076, spanId: 407003698947780173);
            span.Context.TraceContext.Environment = env;
            span.Context.TraceContext.ServiceVersion = version;
            span.SetTraceSamplingPriority(SamplingPriority.AutoKeep);

            var returnedComment = DatabaseMonitoringPropagator.PropagateSpanData(DbmPropagationLevel.Service, "Test.Service", span.Context);

            returnedComment.Should().Be(expectedComment);
        }
    }
}
