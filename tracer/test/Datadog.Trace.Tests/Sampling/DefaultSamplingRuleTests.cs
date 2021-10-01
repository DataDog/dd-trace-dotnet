// <copyright file="DefaultSamplingRuleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Sampling;
using NUnit.Framework;

namespace Datadog.Trace.Tests.Sampling
{
    public class DefaultSamplingRuleTests
    {
        // Value returned by the agent per default
        [TestCase("service:,env:", "hello", "world", 1f)]
        // Does not match
        [TestCase("service:nope,env:nope", "hello", "world", 1f)]
        // Nominal case
        [TestCase("service:hello,env:world", "hello", "world", .5f)]
        // Too many values
        [TestCase("service:hello,env:world,xxxx", "hello", "world", 1f)]
        // ':' in service name
        [TestCase("service:hello:1,env:world", "hello:1", "world", .5f)]
        // ':' in env name
        [TestCase("service:hello,env:world:1", "hello", "world:1", .5f)]
        public void KeyParsing(string key, string expectedService, string expectedEnv, float expectedRate)
        {
            var rule = new DefaultSamplingRule();

            rule.SetDefaultSampleRates(new[] { new KeyValuePair<string, float>(key, .5f) });

            var span = new Span(new SpanContext(1, 1, null, serviceName: expectedService), DateTimeOffset.Now);
            span.SetTag(Tags.Env, expectedEnv);

            var samplingRate = rule.GetSamplingRate(span);

            Assert.AreEqual(expectedRate, samplingRate);
        }
    }
}
