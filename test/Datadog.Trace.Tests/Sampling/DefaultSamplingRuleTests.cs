using System;
using System.Collections.Generic;
using Datadog.Trace.Sampling;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    public class DefaultSamplingRuleTests
    {
        [Theory]
        // Value returned by the agent per default
        [InlineData("service:,env:", "hello", "world", 1f)]
        // Does not match
        [InlineData("service:nope,env:nope", "hello", "world", 1f)]
        // Nominal case
        [InlineData("service:hello,env:world", "hello", "world", .5f)]
        // Too many values
        [InlineData("service:hello,env:world,xxxx", "hello", "world", 1f)]
        // ':' in service name
        [InlineData("service:hello:1,env:world", "hello:1", "world", .5f)]
        // ':' in env name
        [InlineData("service:hello,env:world:1", "hello", "world:1", .5f)]
        public void KeyParsing(string key, string expectedService, string expectedEnv, float expectedRate)
        {
            var rule = new DefaultSamplingRule();

            rule.SetDefaultSampleRates(new[] { new KeyValuePair<string, float>(key, .5f) });

            var span = new Span(new SpanContext(1, 1, null, serviceName: expectedService), DateTimeOffset.Now);
            span.SetTag(Tags.Env, expectedEnv);

            var samplingRate = rule.GetSamplingRate(span);

            Assert.Equal(expectedRate, samplingRate);
        }
    }
}
