using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Dapper;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringBuilderPropagation;
public class StringBuilderBasicTests : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";
    private string _notTaintedValue = "notTaintedValue";
    private string _untaintedString = "UntaintedString";

    public StringBuilderBasicTests()
    {
        AddTainted(_taintedValue);
    }

    // System.Text.StringBuilder::.ctor(System.String)

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilder_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", new StringBuilder(_taintedValue), () => new StringBuilder(_taintedValue));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilder_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString:+-tainted-+:UntaintedString",
            new StringBuilder(_untaintedString + _taintedValue + _untaintedString),
            () => new StringBuilder(_untaintedString + _taintedValue + _untaintedString));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilder_ResultIsNotTainted3()
    {
        AssertUntaintedWithOriginalCallCheck("UntaintedString",
            new StringBuilder(_untaintedString),
            () => new StringBuilder(_untaintedString));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilder_ResultIsNotTainted4()
    {
        AssertUntaintedWithOriginalCallCheck(string.Empty, new StringBuilder(null), () => new StringBuilder(null));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilder_ResultIsTainted2()
    {
        new StringBuilder(null).ToString().Should().Be(String.Empty);
    }

    [Fact]
    public void GivenANotTaintedString_WhenCallingNewStringBuilder_ResultIsNotTainted()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_notTaintedValue).ToString(), 
            () => new StringBuilder(_notTaintedValue).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderToString_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", new StringBuilder(_taintedValue).ToString(), () => new StringBuilder(_taintedValue).ToString());
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
            new StringBuilder(_taintedValue + a1 + a2 + a3 + a4 + a5 + a6).ToString(),
            () => new StringBuilder(_taintedValue + a1 + a2 + a3 + a4 + a5 + a6).ToString());
    }

    [Fact]
    public void GivenANotTaintedString_WhenCallingNewStringBuilderToString_ResultIsNotTainted()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_notTaintedValue).ToString(), 
            () => new StringBuilder(_notTaintedValue).ToString());
    }

    // System.Text.StringBuilder::.ctor(System.String,System.Int32)

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacity_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", new StringBuilder(_taintedValue, 100), () => new StringBuilder(_taintedValue, 100));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacity_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString:+-tainted-+:UntaintedString",
            new StringBuilder(_untaintedString + _taintedValue + _untaintedString, 100),
            () => new StringBuilder(_untaintedString + _taintedValue + _untaintedString, 100));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacity_ResultIsNotTainted3()
    {
        AssertUntaintedWithOriginalCallCheck("UntaintedString",
            new StringBuilder(_untaintedString, 100),
            () => new StringBuilder(_untaintedString, 100));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacity_ResultIsNotTainted4()
    {
        AssertUntaintedWithOriginalCallCheck(string.Empty, new StringBuilder(null, 3), () => new StringBuilder(null, 3));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithMaxLenght_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", new StringBuilder(_taintedValue, 500).ToString(), () => new StringBuilder(_taintedValue, 500).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithMaxLenght_ResultIsTainted4()
    {
        new StringBuilder(null, 500).ToString().Should().Be(String.Empty);
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithWrongMaxLenght_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(_taintedValue, -500));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacity_ResultIsNotTainted5()
    {
        AssertUntaintedWithOriginalCallCheck("UntaintedString",
            new StringBuilder(_untaintedString, 1),
            () => new StringBuilder(_untaintedString, 1));
    }

    // System.Text.StringBuilder::.ctor(System.String,System.Int32,System.Int32,System.Int32)

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacityOffsets_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", new StringBuilder(_taintedValue, 0, 7, 10), () => new StringBuilder(_taintedValue));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacityOffsets_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-in-+:",
            new StringBuilder(_taintedValue, 2, 2, 10),
            () => new StringBuilder(_taintedValue, 2, 2, 10));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacityOffsets_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck("intedString:+-tain-+:",
            new StringBuilder(_untaintedString + _taintedValue + _untaintedString, 4, 15, 22),
            () => new StringBuilder(_untaintedString + _taintedValue + _untaintedString, 4, 15, 22));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacityOffsets_ResultIsNotTainted3()
    {
        AssertUntaintedWithOriginalCallCheck("ta",
            new StringBuilder(_untaintedString, 2, 2, 2),
            () => new StringBuilder(_untaintedString, 2, 2, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacityOffsets_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(null, 2, 2, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacityOffsets_ExceptionIsThrown2()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(_taintedValue, -1, 3, 3));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacityOffsets_ExceptionIsThrown3()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(_taintedValue, 1, -3, 3));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacityOffsets_ExceptionIsThrown4()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(_taintedValue, 1, 3, -3));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacityOffsets_ExceptionIsThrown5()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(_taintedValue, 1, 300, 3));
    }

    [Fact]
    public void GivenATaintedString_WhenCreateStringBuilderCapacityOffsets_ExceptionIsThrown6()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(_taintedValue, 100, 3, 3));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithSubString_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-inte-+:", new StringBuilder(_taintedValue, 2, 4, 100).ToString(), () => new StringBuilder(_taintedValue, 2, 4, 100).ToString());
    }

    // System.Text.StringBuilder::ToString(System.Int32,System.Int32)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderToString_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ta-+:",
            new StringBuilder(_taintedValue).ToString(0, 2),
            () => new StringBuilder(_taintedValue).ToString(0, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderToString_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-in-+:",
            new StringBuilder(_taintedValue).ToString(2, 2),
            () => new StringBuilder(_taintedValue).ToString(2, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderToString_ResultIsTainted3()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(_taintedValue).ToString(-1, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderToString_ResultIsTainted4()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(_taintedValue).ToString(1, -2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderToString_ResultIsTainted5()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(_taintedValue).ToString(1, 332));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderWithWrongSubString_ArgumentOutOfRangeException6()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(_taintedValue).ToString(200, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingNewStringBuilderToString2Params_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-int-+:", new StringBuilder(_taintedValue).ToString(2, 3), () => new StringBuilder(_taintedValue).ToString(2, 3));
    }

    // Ranges validation

    [Fact]
    public void GivenAStringBuilder_WhenUsingNotCoveredMethod_RangesAreOk()
    {
        var stringBuilder = new StringBuilder().Append(_taintedValue);
        MethodInfo replaceMethod = typeof(StringBuilder).GetMethod("Replace", new Type[] {typeof(string), typeof(string) });
        replaceMethod.Invoke(stringBuilder, new object[] { _taintedValue, "a" });
        var mystring = stringBuilder.ToString();
        ValidateRanges(mystring);
    }

    [Fact]
    public void GivenAStringBuilder_WhenUsingNotCoveredMethod_RangesAreOk2()
    {
        var stringBuilder = new StringBuilder().Append(_taintedValue + _taintedValue);
        MethodInfo replaceMethod = typeof(StringBuilder).GetMethod("Replace", new Type[] { typeof(string), typeof(string) });
        replaceMethod.Invoke(stringBuilder, new object[] { _taintedValue + _taintedValue, _taintedValue });
        var mystring = stringBuilder.ToString();
        ValidateRanges(mystring);
    }

    [Fact]
    public void GivenAStringBuilder_WhenUsingNotCoveredMethod_RangesAreOk3()
    {
        var stringBuilder = new StringBuilder(_taintedValue);
        for (int i = 0; i < 100; i++)
        {
            stringBuilder.Append(_taintedValue);
        }

        MethodInfo replaceMethod = typeof(StringBuilder).GetMethod("Replace", new Type[] { typeof(string), typeof(string) });
        replaceMethod.Invoke(stringBuilder, new object[] { _taintedValue, string.Empty});

        stringBuilder.Append(_taintedValue);
        stringBuilder.Append(_taintedValue, 2, 3);
#if !NETFRAMEWORK
        stringBuilder.AppendJoin("s", new object[] { _taintedValue });
#endif
        stringBuilder.Remove(2, 2);
        stringBuilder.Replace("ta", "TA");
        stringBuilder.AppendFormat("{0} {1}", _taintedValue, _taintedValue);
        stringBuilder.Insert(4, _taintedValue);
        var mystring = stringBuilder.ToString();
        ValidateRanges(mystring);
    }

    [Fact]
    public void GivenAStringBuilder_WhenUsingNotCoveredMethod_RangesAreOk4()
    {
        var stringBuilder = new StringBuilder().Append("aw" + _taintedValue);
        MethodInfo replaceMethod = typeof(StringBuilder).GetMethod("Replace", new Type[] { typeof(string), typeof(string) });
        replaceMethod.Invoke(stringBuilder, new object[] { "wtainted" , string.Empty});
        var mystring = stringBuilder.ToString();
        ValidateRanges(mystring);
    }

    [Fact]
    public void GivenAStringBuilder_WhenUsingNotCoveredMethod_RangesAreOk5()
    {
        var stringBuilder = new StringBuilder().Append("aw" + _taintedValue + _taintedValue);
        MethodInfo replaceMethod = typeof(StringBuilder).GetMethod("Replace", new Type[] { typeof(string), typeof(string) });
        replaceMethod.Invoke(stringBuilder, new object[] { "wtainted", string.Empty });
        var mystring = stringBuilder.ToString();
        ValidateRanges(mystring);
        FormatTainted(mystring).Should().Be("at:+-ainted-+:");
    }

    [Fact]
    public void GivenAStringBuilder_WhenUsingNotCoveredMethod_RangesAreOk6()
    {
        var stringBuilder = new StringBuilder().Append(_taintedValue + "aw" + _taintedValue);
        MethodInfo replaceMethod = typeof(StringBuilder).GetMethod("Replace", new Type[] { typeof(string), typeof(string) });
        replaceMethod.Invoke(stringBuilder, new object[] { "wtainted", string.Empty });
        var mystring = stringBuilder.ToString();
        ValidateRanges(mystring);
        FormatTainted(mystring).Should().Be(":+-tainted-+:a");
    }

    // SetLenght

    [Fact]
    public void GivenATaintedString_WhenChangingLength_ResultIsTainted()
    {
        var stringBuilder = new StringBuilder(_taintedValue);
        stringBuilder.Length = 2;
        FormatTainted(stringBuilder.ToString()).Should().Be(":+-ta-+:");
    }

    [Fact]
    public void GivenATaintedString_WhenChangingLength_ResultIsTainted2()
    {
        var stringBuilder = new StringBuilder(_taintedValue);
        stringBuilder.Length = 10;
        AssertTainted(stringBuilder);
    }

    [Fact]
    public void GivenATaintedString_WhenChangingLength_ResultIsTainted4()
    {
        var stringBuilder = new StringBuilder(_taintedValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => stringBuilder.Length = -1);
    }
}
