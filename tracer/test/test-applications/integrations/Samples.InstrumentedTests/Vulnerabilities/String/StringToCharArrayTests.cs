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
    public void GivenATaintedObject_WhenCallingToCharArrayWithWrongIndex_ExceptionIsThrown()
    {
        var result = taintedValue.ToCharArray(3, 2);
        result.Count().Should().Be(2);
        AssertTainted(result);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToCharArrayWithWrongIndex_ExceptionIsThrown2()
    {
        var result = taintedValue.ToCharArray(-1, 2);
        result.Count().Should().Be(2);
        AssertTainted(result);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToCharArrayWithWrongIndex_ExceptionIsThrown3()
    {
        var result = taintedValue.ToCharArray(-1, 2000);
        result.Count().Should().Be(2);
        AssertTainted(result);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToCharArrayWithWrongIndex_ExceptionIsThrown4()
    {
        var result = taintedValue.ToCharArray(1, 2000);
        result.Count().Should().Be(2);
        AssertTainted(result);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToCharArrayWithIndex_ResultIsTainted()
    {
        var result = taintedValue.ToCharArray(1, -2000);
        result.Count().Should().Be(2);
        AssertTainted(result);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToCharArray_ResultIsTainted()
    {
        var result = taintedValue.ToCharArray();
        result.Count().Should().Be(taintedValue.Length);
        AssertTainted(result);
    }
}
