#if NET6_0_OR_GREATER

using System;
using Xunit;
using System.Runtime.CompilerServices;
using FluentAssertions;

namespace Samples.InstrumentedTests.Iast.Propagation.String;

public class SpanStringTests : InstrumentationTestsBase
{
    protected string TaintedValue = "tainted";
    protected string UntaintedValue = "untainted";

    public SpanStringTests()
    {
        AddTainted(TaintedValue);
    }

    [Fact]
    public void GivenAnTaintedString_WhenCastingToSpan_SpanIsTainted()
    {
        ReadOnlySpan<char> taintedSpan = TaintedValue;
        ReadOnlySpan<char> nonTaintedSpan = UntaintedValue;

        AssertTainted(ref taintedSpan);
        AssertNotTainted(ref nonTaintedSpan);
    }
}

#endif
