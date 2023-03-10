using System;
using System.Security.Cryptography;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;

public class StringSubstringTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string taintedValue2 = "TAINTED2";
    protected string TaintedString = "TaintedString";
    protected string UntaintedString = "UntaintedString";
    protected string OtherTaintedString = "OtherTaintedString";
    protected string OtherUntaintedString = "OtherUntaintedString";
    protected object UntaintedObject = "UntaintedObject";
    protected object TaintedObject = "TaintedObject";
    protected object OtherTaintedObject = "OtherTaintedObject";
    protected object OtherUntaintedObject = "OtherUntaintedObject";

    public StringSubstringTests()
    {
        AddTainted(taintedValue);
        AddTainted(taintedValue2);
        AddTainted(TaintedObject);
        AddTainted(OtherTaintedObject);
        AddTainted(TaintedString);
        AddTainted(OtherTaintedString);
    }

    [Fact]
    public void GivenATaintedString_WhenSubstring_ThenResultIsTainted()
    {
        string testString1 = AddTaintedString("0123456789");

        FormatTainted(testString1.Substring(0)).Should().Be(":+-0123456789-+:");
        FormatTainted(testString1.Substring(5)).Should().Be(":+-56789-+:");
        FormatTainted(testString1.Substring(5, 3)).Should().Be(":+-567-+:");
        FormatTainted(testString1.Substring(8, 2)).Should().Be(":+-89-+:");

        var string1 = AddTaintedString("abc");
        var string2 = AddTaintedString("123");
        var string3 = AddTaintedString("ABC");

        string testString2 = "(" + string1 + ")" + "[" + string2 + "]" + "{" + string3 + "}";
        FormatTainted(testString2).Should().Be("(:+-abc-+:)[:+-123-+:]{:+-ABC-+:}");
        FormatTainted(testString2.Substring(0)).Should().Be("(:+-abc-+:)[:+-123-+:]{:+-ABC-+:}");
        FormatTainted(testString2.Substring(5, 5)).Should().Be("[:+-123-+:]");
        FormatTainted(testString2.Substring(3, 5)).Should().Be(":+-c-+:)[:+-12-+:");
        FormatTainted(testString2.Substring(7, 5)).Should().Be(":+-23-+:]{:+-A-+:");
        FormatTainted(testString2.Substring(10, 4)).Should().Be("{:+-ABC-+:");
    }

    [Fact]
    public void GivenATaintedString_WhenSubstringIndexWithTainted_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-intedString-+:", TaintedString.Substring(2), () => TaintedString.Substring(2));
    }

    [Fact]
    public void GivenATaintedString_WhenSubstringIndexWithTainted_ThenResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:", TaintedString.Substring(0), () => TaintedString.Substring(0));
    }

    [Fact]
    public void GivenATaintedString_WhenSubstringIndexWithTainted_ThenResultIsTainted3()
    {
        TaintedString.Substring(13).Should().Be(string.Empty);
    }

    [Fact]
    public void GivenATaintedString_WhenSubstringIndexWithTainted_ThenResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-Tai-+:", TaintedString.Substring(0, 3), () => TaintedString.Substring(0, 3));
    }

    [Fact]
    public void GivenATaintedString_WhenSubstringIndexWithBoth_ThenResultIsTainted()
    {
        string str = String.Concat(TaintedString, UntaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-intedString-+:UntaintedString", str.Substring(2), () => str.Substring(2));
    }

    [Fact]
    public void GivenATaintedString_WhenSubstringIndexWithBoth_ThenResultIsTainted2()
    {
        string str = String.Concat(TaintedString, UntaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-intedString-+:Un", str.Substring(2, 13), () => str.Substring(2, 13));
    }

    [Fact]
    public void GivenATaintedString_WhenSubstringIndexWithBothInverse_ThenResultIsTainted()
    {
        string str = String.Concat(UntaintedString, TaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-intedString-+:", str.Substring(17), () => str.Substring(17));
    }

    [Fact]
    public void GivenATaintedString_WhenSubstringIndexWithTwoTainted_ThenResultIsTainted()
    {
        string str = String.Concat(String.Concat(TaintedString, UntaintedString), TaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-intedString-+:UntaintedString:+-TaintedString-+:", str.Substring(2), () => str.Substring(2));
    }

    [Fact]
    public void GivenATaintedString_WhenSubstringIndexWithTwoTaintedTwoUntainted_ThenResultIsTainted()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-String-+:UntaintedString:+-OtherTaintedString-+:OtherUntaintedString", str.Substring(7), () => str.Substring(7));
    }

    [Fact]
    public void GivenATaintedString_WhenSubstringIndexTaintedBaseOutOfRange_ThenResultIsNotTainted()
    {
        string str = String.Concat(TaintedString, UntaintedString);
        string newString = str.Substring(13);

        Assert.Equal("UntaintedString", newString);
        AssertNotTainted(newString);
    }

    [Fact]
    public void GivenATaintedString_WhenCallingSubstringWithOneParameter_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-inted-+:", taintedValue.Substring(2), () => taintedValue.Substring(2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingSubstringWithTwoParameters_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-in-+:", taintedValue.Substring(2, 2), () => taintedValue.Substring(2, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingSubstringWithOneWrongParameter_ThenArgumentOutOfRangeExceptionIsThrown()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => taintedValue.Substring(200));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingSubstringWithTwoWrongParameter_ThenArgumentOutOfRangeExceptionIsThrown()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => taintedValue.Substring(2, 200));
    }

    [Fact]
     public void GivenATaintedString_WhenCallingSubstringWithTwoWrongParameter_ThenArgumentOutOfRangeExceptionIsThrown2()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => taintedValue.Substring(2200, 1));
    }
}
