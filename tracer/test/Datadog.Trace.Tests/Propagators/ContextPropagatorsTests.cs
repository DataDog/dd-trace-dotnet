// <copyright file="ContextPropagatorsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Propagators;
using Xunit;

namespace Datadog.Trace.Tests.Propagators
{
    public class ContextPropagatorsTests
    {
        [Theory]
        [InlineData(new[] { "Datadog" }, new[] { "Datadog" })]
        [InlineData(new[] { "Datadog" }, new[] { "Datadog, B3" })]
        [InlineData(new[] { "Datadog, B3" }, new[] { "Datadog, B3" })]
        [InlineData(new[] { "Datadog, W3C" }, new[] { "Datadog, B3" })]
        [InlineData(new[] { "Datadog, W3C" }, new[] { "Datadog, W3C" })]
        [InlineData(new[] { "Datadog, ERROR" }, new[] { "Datadog, W3C" })]
        [InlineData(new[] { "Datadog" }, new[] { "W3C" })]
        [InlineData(new[] { "ERROR" }, new[] { "ERROR" })]
        public void TestCombinations(string[] injectors, string[] extractors)
        {
            var propagator = ContextPropagators.GetSpanContextPropagator(injectors, extractors);
            Assert.NotNull(propagator);
        }
    }
}
