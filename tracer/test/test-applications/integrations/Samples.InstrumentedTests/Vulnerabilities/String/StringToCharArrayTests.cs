using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;
public class StringToCharArrayTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    public StringToCharArrayTests()
    {
        AddTainted(taintedValue);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToCharArrayWithWrongIndex_IsTainted()
    {
        var result = taintedValue.ToCharArray(3, 2);
        result.Count().Should().Be(2);
        result[0].Should().Be('n');
        result[1].Should().Be('t');
        AssertTainted(result);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToCharArrayWithWrongIndex_ExceptionIsThrown2()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => taintedValue.ToCharArray(-1, 2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToCharArrayWithWrongIndex_ExceptionIsThrown3()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => taintedValue.ToCharArray(-1, 2000));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToCharArrayWithWrongIndex_ExceptionIsThrown4()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => taintedValue.ToCharArray(1, 2000));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToCharArrayWithWrongIndex_ExceptionIsThrown5()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => taintedValue.ToCharArray(1, -2000));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToCharArray_ResultIsTainted()
    {
        var result = taintedValue.ToCharArray();
        result.Count().Should().Be(taintedValue.Length);
        AssertTainted(result);
    }
}
