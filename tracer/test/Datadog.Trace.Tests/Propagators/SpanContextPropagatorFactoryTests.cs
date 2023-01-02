// <copyright file="SpanContextPropagatorFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Propagators;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Propagators;

public class SpanContextPropagatorFactoryTests
{
    [Theory]
    [InlineData("invalid")]
    [InlineData("1, 2, 3")]
    [InlineData("")]
    [InlineData(null)]
    public void InvalidContextPropagator(string headerStyles)
    {
        var propagators = SpanContextPropagatorFactory.GetPropagators<IContextExtractor>(headerStyles?.Split(','));

        propagators.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void MixOfValidAndInvalidContextPropagator()
    {
        string[] headerStyles = { "1", "datadog", "3", "tracecontext" };
        var propagators = SpanContextPropagatorFactory.GetPropagators<IContextExtractor>(headerStyles).ToArray();

        propagators.Should().NotBeNull().And.HaveCount(2);
        propagators[0].Should().BeSameAs(Datadog.Trace.Propagators.DatadogContextPropagator.Instance);
        propagators[1].Should().BeSameAs(Datadog.Trace.Propagators.W3CTraceContextPropagator.Instance);
    }

    [Theory]
    [InlineData("Datadog")]
    [InlineData("datadog")]         // case-insensitive
    [InlineData("Datadog,datadog")] // multiple entries return one instance
    public void DatadogContextPropagator(string headerStyles)
    {
        var propagators = SpanContextPropagatorFactory.GetPropagators<IContextExtractor>(headerStyles.Split(','));

        propagators.Should().HaveCount(1).And.AllBeOfType<DatadogContextPropagator>();
    }

    [Theory]
    [InlineData("tracecontext")]
    [InlineData("TraceContext")]              // case-insensitive
    [InlineData("tracecontext,TraceContext")] // multiple entries returns one instance
    [InlineData("W3C")]                       // deprecated value
    public void W3CTraceContextPropagator(string headerStyles)
    {
        var propagators = SpanContextPropagatorFactory.GetPropagators<IContextExtractor>(headerStyles.Split(','));

        propagators.Should().HaveCount(1).And.AllBeOfType<W3CTraceContextPropagator>();
    }

    [Theory]
    [InlineData("b3multi")]
    [InlineData("B3Multi")]         // case-insensitive
    [InlineData("b3multi,B3Multi")] // multiple entries returns one instance
    [InlineData("b3")]              // deprecated value
    public void B3MultipleHeaderContextPropagator(string headerStyles)
    {
        var propagators = SpanContextPropagatorFactory.GetPropagators<IContextExtractor>(headerStyles.Split(','));

        propagators.Should().HaveCount(1).And.AllBeOfType<B3MultipleHeaderContextPropagator>();
    }

    [Theory]
    [InlineData("b3 single header")]
    [InlineData("B3 Single Header")]                  // case-insensitive
    [InlineData("b3 single header,B3 Single Header")] // multiple entries returns one instance
    [InlineData("b3singleheader")]                    // deprecated value
    public void B3SingleHeaderContextPropagator(string headerStyles)
    {
        var propagators = SpanContextPropagatorFactory.GetPropagators<IContextExtractor>(headerStyles.Split(','));

        propagators.Should().HaveCount(1).And.AllBeOfType<B3SingleHeaderContextPropagator>();
    }
}
