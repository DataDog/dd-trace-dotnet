// <copyright file="DatabaseMonitoringTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    [Trait("RequiresDockerDependency", "true")]
    public class DatabaseMonitoringTests : TracingIntegrationTest
    {
        public DatabaseMonitoringTests(ITestOutputHelper output)
            : base("MySql", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable("DD_ENV", "testing");
            SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", "full");
        }

        public static IEnumerable<object[]> GetMySql8Data()
        {
            foreach (object[] item in PackageVersions.MySqlData)
            {
                if (!((string)item[0]).StartsWith("8") && !string.IsNullOrEmpty((string)item[0]))
                {
                    continue;
                }

                yield return item;
            }
        }

        public override Result ValidateIntegrationSpan(MockSpan span) => span.IsMySql();

        // Check that the span has been tagged after the comment was propagated
        private void ValidateDbmTaggedSpans(IEnumerable<MockSpan> spans)
        {
            foreach (var span in spans)
            {
                Console.WriteLine(span.ToString());
                Assert.True(span.Tags?.ContainsKey(Tags.DbmDataPropagated));
            }
        }

        [Fact]
        private void ConfirmDbmPropagateSpanData()
        {
            const string expectedServiceComment = "/*ddps='Test.Service',dddbs='Test.Service-mysql',ddpv='1.0.0',dde='testing'*/";
            const string expectedTraceParent = "traceparent='00-00000000000000006172c1c9a829c71c-05a5f7b5320d6e4d-01'";
            const string expectedFullComment = "/*ddps='Test.Service',dddbs='Test.Service-mysql',ddpv='1.0.0',dde='testing',traceparent='00-00000000000000006172c1c9a829c71c-05a5f7b5320d6e4d-01'*/";

            Span span = Tracer.Instance.StartSpan("mysql.query", null, null, "Test.Service-mysql", null, 7021887840877922076, 407003698947780173);
            span.SetTag(Tags.Env, "testing");
            span.SetTag(Tags.Version, "1.0.0");
            span.SetMetric(Tags.SamplingPriority, 0.5);

            var currentServiceComment = DatabaseMonitoringPropagator.PropagateSpanData("service", "Test.Service", span);
            var currentServiceTraceParent = DatabaseMonitoringPropagator.CreateTraceParent(span.TraceId, span.SpanId, span.GetMetric(Tags.SamplingPriority));
            var currentFullComment = DatabaseMonitoringPropagator.PropagateSpanData("full", "Test.Service", span);

            Assert.Equal(expectedServiceComment, currentServiceComment);
            Assert.Equal(expectedTraceParent, currentServiceTraceParent);
            Assert.Equal(expectedFullComment, currentFullComment);
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [MemberData(nameof(GetMySql8Data))]
        private void SubmitDbmCommentedSpans(string packageVersion)
        {
            // ALWAYS: 75 spans
            // - MySqlCommand: 19 spans (3 groups * 7 spans - 2 missing spans)
            // - DbCommand:  42 spans (6 groups * 7 spans)
            // - IDbCommand: 14 spans (2 groups * 7 spans)
            //
            // NETSTANDARD: +56 spans
            // - DbCommand-netstandard:  42 spans (6 groups * 7 spans)
            // - IDbCommand-netstandard: 14 spans (2 groups * 7 spans)
            //
            // CALLTARGET: +9 spans
            // - MySqlCommand: 2 additional spans
            // - IDbCommandGenericConstrant<MySqlCommand>: 7 spans (1 group * 7 spans)
            //
            // NETSTANDARD + CALLTARGET: +7 spans
            // - IDbCommandGenericConstrant<MySqlCommand>-netstandard: 7 spans (1 group * 7 spans)
            var expectedSpanCount = 147;

            const string dbType = "mysql";
            const string expectedOperationName = dbType + ".query";

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
            int actualSpanCount = spans.Count(s => s.ParentId.HasValue && !s.Resource.Equals("SHOW WARNINGS", StringComparison.OrdinalIgnoreCase)); // Remove unexpected DB spans from the calculation

            Assert.Equal(expectedSpanCount, actualSpanCount);
            ValidateDbmTaggedSpans(spans);
        }
    }
}
