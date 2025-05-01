// <copyright file="SamplingRuleQuickTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    [Collection(nameof(Datadog.Trace.Tests.Sampling))]
    public class SamplingRuleQuickTest
    {
        [Theory]
        [InlineData("select top ( @count ) Value from [ PRODUCTION.Scheduler ] . [ Set ] with ( forceseek ) where [ Key ] = @key and Score between @from and @to order by Score", 0, 0.1)]
        [InlineData("select count ( j.Id ) from ( select top ( @limit ) Id from [ PRODUCTION.Scheduler ] . Job with ( nolock, forceseek ) where StateName = @state )", 0, 0.1)]
        [InlineData("set nocount on set xact_abort on set tran isolation level read committed update top ( ? ) JQ set FetchedAt = GETUTCDATE ( ) output INSERTED.Id, INSERTED.JobId, INSERTED.Queue, INSERTED.FetchedAt from [ PRODUCTION.Scheduler ] . JobQueue JQ with ( forceseek, readpast, updlock, rowlock ) where Queue in ( @queues0 ) and ( FetchedAt is ? or FetchedAt < DATEADD ( second, @timeoutSs, GETUTCDATE ( ) ) )", 0, 0.1)]
        [InlineData("foo", 1, 0.5)]
        public void Constructs_With_ResourceName_Remote_Foo(string resource, int expectedRuleMatchIndex, double expectedSamplingRate)
        {
            const string configJson = """
                                      [
                                        { "service": "daybreak-worker", resource: "*PRODUCTION.Scheduler*", "sample_rate": 0.1 },
                                        { "service": "daybreak-worker", "sample_rate": 0.5 },
                                      ]
                                      """;

            // parse sampling rules from json
            var regexTimeout = TimeSpan.FromMilliseconds(200);
            var samplingRules = RemoteCustomSamplingRule.BuildFromConfigurationString(configJson, regexTimeout);

            // create sampler and register sampling rules
            var rateLimiter = new TracerRateLimiter(maxTracesPerInterval: -1, intervalMilliseconds: null); // -1 disables rate-limiting
            var sampler = new TraceSampler(rateLimiter);
            sampler.RegisterRules(samplingRules);

            var span = new Span(new SpanContext(1, 1, serviceName: "daybreak-worker"), DateTimeOffset.Now) { ResourceName = resource };

            // assert that the expected sampling rule is matched (by index)
            samplingRules[expectedRuleMatchIndex].IsMatch(span).Should().BeTrue();

            _ = sampler.MakeSamplingDecision(span);
            var appliedSamplingRate = span.GetMetric(Metrics.SamplingRuleDecision);

            // can't check for exact equality because of floating point precision.
            // for example, we may see 0.10000000149011612 for 0.1.
            appliedSamplingRate.Should().BeInRange(expectedSamplingRate - 0.00001, expectedSamplingRate + 0.00001);
        }
    }
}
