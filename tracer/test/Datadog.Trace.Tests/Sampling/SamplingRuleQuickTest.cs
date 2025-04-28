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
        [InlineData("select top ( @count ) Value from [ PRODUCTION.Scheduler ] . [ Set ] with ( forceseek ) where [ Key ] = @key and Score between @from and @to order by Score")]
        [InlineData("select count ( j.Id ) from ( select top ( @limit ) Id from [ PRODUCTION.Scheduler ] . Job with ( nolock, forceseek ) where StateName = @state )")]
        [InlineData("set nocount on set xact_abort on set tran isolation level read committed update top ( ? ) JQ set FetchedAt = GETUTCDATE ( ) output INSERTED.Id, INSERTED.JobId, INSERTED.Queue, INSERTED.FetchedAt from [ PRODUCTION.Scheduler ] . JobQueue JQ with ( forceseek, readpast, updlock, rowlock ) where Queue in ( @queues0 ) and ( FetchedAt is ? or FetchedAt < DATEADD ( second, @timeoutSs, GETUTCDATE ( ) ) )")]
        public void Constructs_With_ResourceName_Remote_Foo(string resource)
        {
            const string config = """[{ "service": "daybreak-worker", resource: "*PRODUCTION.Scheduler*", "sample_rate":0.1 }]""";
            var timeout = TimeSpan.FromMilliseconds(200);
            var samplingRule = RemoteCustomSamplingRule.BuildFromConfigurationString(config, timeout).Single();

            var span = new Span(new SpanContext(1, 1, serviceName: "daybreak-worker"), DateTimeOffset.Now) { ResourceName = resource };
            samplingRule.GetSamplingRate(span).Should().Be(0.1f);
            samplingRule.IsMatch(span).Should().BeTrue();
        }
    }
}
