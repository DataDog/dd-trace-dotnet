using System;
using System.Text;
using Xunit;
namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringBuilderPropagation;

public class StringBuilderCopyToTests : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";

    public StringBuilderCopyToTests()
    {
        AddTainted(_taintedValue);
    }

    [Fact]
    public void GivenATaintedString_WhenCallingCopyTo_ResultIsTainted()
    {
        char[] result = new char[20];
        new StringBuilder(_taintedValue).CopyTo(0, result, 0, 3);
        AssertTainted(result);
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GivenATaintedString_WhenCallingCopyToWrongArguments_ArgumentOutOfRangeException()
    {
        char[] result = new char[20];
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).CopyTo(-1, result, 0, 3), 
            () => new StringBuilder(_taintedValue).CopyTo(-1, result, 0, 3));
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GivenATaintedString_WhenCallingCopyToWrongArguments_ArgumentOutOfRangeException2()
    {
        char[] result = new char[20];
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).CopyTo(1000, result, 0, 3), 
            () => new StringBuilder(_taintedValue).CopyTo(1000, result, 0, 3));
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GivenATaintedString_WhenCallingCopyToWrongArguments_ArgumentOutOfRangeException3()
    {
        char[] result = new char[20];
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).CopyTo(1, result, -1, 3), 
            () => new StringBuilder(_taintedValue).CopyTo(1, result, -1, 3));
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentException))]
    public void GivenATaintedString_WhenCallingCopyToWrongArguments_ArgumentException()
    {
        char[] result = new char[20];
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).CopyTo(1, result, 1000, 3), 
            () => new StringBuilder(_taintedValue).CopyTo(1, result, 1000, 3));
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GivenATaintedString_WhenCallingCopyToWrongArguments_ArgumentOutOfRangeException5()
    {
        char[] result = new char[20];
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).CopyTo(1, result, 0, -3), 
            () => new StringBuilder(_taintedValue).CopyTo(1, result, 0, -3));
    }

    [Fact]    
    //[ExpectedException(typeof(ArgumentException))]
    public void GivenATaintedString_WhenCallingCopyToWrongArguments_ArgumentException2()
    {
        char[] result = new char[20];
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).CopyTo(1, result, 0, 1000), 
            () => new StringBuilder(_taintedValue).CopyTo(1, result, 0, 1000));
    }
}

