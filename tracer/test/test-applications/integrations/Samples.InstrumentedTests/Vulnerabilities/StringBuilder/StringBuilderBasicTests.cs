using System;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringBuilderPropagation;
public class StringBuilderBasicTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    string notTaintedValue = "notTaintedValue";

    public StringBuilderBasicTests()
    {
        AddTainted(taintedValue);
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilder_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", new StringBuilder(taintedValue).ToString(), () => new StringBuilder(taintedValue).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilder_ResultIsTainted2()
    {
        new StringBuilder(null).ToString().Should().Be(String.Empty);
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithMaxLenght_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", new StringBuilder(taintedValue, 500).ToString(), () => new StringBuilder(taintedValue, 500).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithMaxLenght_ResultIsTainted4()
    {
        new StringBuilder(null, 500).ToString().Should().Be(String.Empty);
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithWrongMaxLenght_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue, -500));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithSubString_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-inte-+:", new StringBuilder(taintedValue, 2, 4, 100).ToString(), () => new StringBuilder(taintedValue, 2, 4, 100).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithWrongSubString_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue, -2, 4, 100).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithWrongSubString_ArgumentOutOfRangeException2()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue, 200, 4, 100));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithWrongSubString_ArgumentOutOfRangeException3()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue, 2, -4, 100));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithWrongSubString_ArgumentOutOfRangeException4()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue, 2, 4, -100));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithWrongSubString_ArgumentOutOfRangeException5()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(null, 2, 4, 100).ToString());
    }

    [Fact]
    public void GivenANotTaintedString_WhenCallingNewStringBuilder_ResultIsNotTainted()
    {
        AssertNotTaintedWithOriginalCallCheck(new StringBuilder(notTaintedValue).ToString(), () => new StringBuilder(notTaintedValue).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderToString_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", new StringBuilder(taintedValue).ToString(), () => new StringBuilder(taintedValue).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingLocalStringBuilderToString_ResultIsTainted()
    {
        string a1 = "1";
        string a2 = "2";
        string a3 = "3";
        string a4 = "4";
        string a5 = "5";
        string a6 = "6";
        
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:123456", 
            new StringBuilder(taintedValue + a1 + a2 + a3 + a4 + a5 + a6).ToString(), 
            () => new StringBuilder(taintedValue + a1 + a2 + a3 + a4 + a5 + a6).ToString());
    }

    [Fact]
    public void GivenANotTaintedString_WhenCallingNewStringBuilderToString_ResultIsNotTainted()
    {
        AssertNotTaintedWithOriginalCallCheck(new StringBuilder(notTaintedValue).ToString(), () => new StringBuilder(notTaintedValue).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderToString2Params_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-int-+:", new StringBuilder(taintedValue).ToString(2, 3), () => new StringBuilder(taintedValue).ToString(2, 3));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithWrongSubString_ArgumentOutOfRangeException6()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).ToString(200, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithWrongSubString_ArgumentOutOfRangeException7()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).ToString(-200, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithWrongSubString_ArgumentOutOfRangeException8()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).ToString(2, 200));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithWrongSubString_ArgumentOutOfRangeException9()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).ToString(2, -2));
    }
}
