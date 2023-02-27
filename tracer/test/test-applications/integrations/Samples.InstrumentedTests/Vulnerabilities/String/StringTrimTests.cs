using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;
public class StringTrimTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string taintedValueSpaces = " trimtext ";
    protected string TaintedString = "TaintedString";

    public StringTrimTests()
    {
        AddTainted(taintedValue);
        AddTainted(taintedValueSpaces);
        AddTainted(TaintedString);
    }

    [Fact]
    public void GivenATaintedString_WhenCallingTrim_ResultIsTainted()
    {
        string testString1 = AddTaintedString("01234");
        FormatTainted(testString1.Trim()).Should().Be(":+-01234-+:");

        string testString2 = AddTaintedString("  01234  ");
        FormatTainted(testString2.Trim()).Should().Be(":+-01234-+:");

        string testString3 = AddTaintedString("   012   ");
        FormatTainted(testString3.Trim()).Should().Be(":+-012-+:");
    }

    [Fact]
    public void GivenATaintedString_WhenCallingTrim_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext-+:", String.Concat(TaintedString, "   ").Trim(), () => String.Concat(TaintedString, "   ").Trim());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingTrim_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext-+:", String.Concat("   ", TaintedString).Trim(), () => String.Concat("   ", TaintedString).Trim());
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimWithoutParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext-+:", taintedValueSpaces.Trim(), () => (taintedValueSpaces).Trim());
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimWithoutParams_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ainted-+:",taintedValue.Trim('t'), () => (taintedValue).Trim('t'));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimWithCharArrayParam_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ainte-+:", taintedValue.Trim(new char[] { 't', 'd' }), () => taintedValue.Trim(new char[] { 't', 'd' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimWithNullParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext-+:", (taintedValueSpaces).Trim(null), () => (taintedValueSpaces).Trim(null));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimEnd_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+- trimtext-+:", taintedValueSpaces.TrimEnd(' '), () => (taintedValueSpaces).TrimEnd(' '));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimEndCharArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+- trimtext-+:", taintedValueSpaces.TrimEnd(new char[] { ' ' }), () => (taintedValueSpaces).TrimEnd(new char[] { ' ' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimEndWithNullParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+- trimtext-+:", taintedValueSpaces.TrimEnd(null), () => (taintedValueSpaces).TrimEnd(null));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimEndNoParameters_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+- trimtext-+:", taintedValueSpaces.TrimEnd(), () => (taintedValueSpaces).TrimEnd());
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimStart_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext -+:", taintedValueSpaces.TrimStart(' '), () => (taintedValueSpaces).TrimStart(' '));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimStartCharArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext -+:", taintedValueSpaces.TrimStart(new char[] { ' ' }), () => (taintedValueSpaces).TrimStart(new char[] { ' ' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimStartNoParameters_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext -+:", taintedValueSpaces.TrimStart(), () => (taintedValueSpaces).TrimStart());
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimStartWithNullParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext -+:", taintedValueSpaces.TrimStart(null), () => (taintedValueSpaces).TrimStart(null));
    }

}
