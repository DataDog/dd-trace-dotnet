// <copyright file="DatabaseMonitoringPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.DatabaseMonitoring;
using Datadog.Trace.ExtensionMethods;
using Xunit;

namespace Datadog.Trace.Tests.DatabaseMonitoring
{
    public class DatabaseMonitoringPropagatorTests
    {
        [Theory]
        [InlineData("disabled", SamplingPriority.UserKeep, "")]
        [InlineData("Service", SamplingPriority.AutoReject, "/*ddps='Test.Service',dddbs='Test.Service-mysql',ddpv='1.0.0',dde='testing'*/")]
        [InlineData("fUll", SamplingPriority.AutoKeep, "/*ddps='Test.Service',dddbs='Test.Service-mysql',ddpv='1.0.0',dde='testing',traceparent='00-00000000000000006172c1c9a829c71c-05a5f7b5320d6e4d-01'*/")]
        [InlineData("full", SamplingPriority.UserReject, "/*ddps='Test.Service',dddbs='Test.Service-mysql',ddpv='1.0.0',dde='testing',traceparent='00-00000000000000006172c1c9a829c71c-05a5f7b5320d6e4d-00'*/")]
        public void ExpectedCommentInjected(string propagationMode, SamplingPriority samplingPriority, string expectedComment)
        {
            DbmPropagationLevel dbmPropagationLevel;
            Enum.TryParse(propagationMode, true, out dbmPropagationLevel);

            Span span = Tracer.Instance.StartSpan("mysql.query", null, null, "Test.Service-mysql", null, 7021887840877922076, 407003698947780173);
            span.SetTag(Tags.Env, "testing");
            span.SetTag(Tags.Version, "1.0.0");
            span.SetTraceSamplingPriority(samplingPriority);

            var returnedComment = DatabaseMonitoringPropagator.PropagateSpanData(dbmPropagationLevel, "Test.Service", span);

            Assert.Equal(expectedComment, returnedComment);
        }

        [Theory]

        [InlineData("/*ddps='Test.Service',dddbs='Test.Service-mysql',ddpv='1.0.0',dde='testing'*/", "testing", "1.0.0")]
        [InlineData("/*ddps='Test.Service',dddbs='Test.Service-mysql',ddpv='1.0.0'*/", null, "1.0.0")]
        [InlineData("/*ddps='Test.Service',dddbs='Test.Service-mysql'*/", null, null)]
        [InlineData("/*ddps='Test.Service',dddbs='Test.Service-mysql',dde='testing'*/", "testing", null)]
        public void ExpectedTagsInjected(string expectedComment, string env = null, string version = null)
        {
            Span span = Tracer.Instance.StartSpan("mysql.query", null, null, "Test.Service-mysql", null, 7021887840877922076, 407003698947780173);
            span.SetTag(Tags.Env, env);
            span.SetTag(Tags.Version, version);
            span.SetTraceSamplingPriority(SamplingPriority.AutoKeep);

            var returnedComment = DatabaseMonitoringPropagator.PropagateSpanData(DbmPropagationLevel.Service, "Test.Service", span);

            Assert.Equal(expectedComment, returnedComment);
        }
    }
}
