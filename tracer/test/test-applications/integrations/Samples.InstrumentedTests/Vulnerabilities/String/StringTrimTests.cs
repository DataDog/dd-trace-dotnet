using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;
public class StringTrimTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string taintedValueSpaces = " trimtext ";
    protected string taintedValueSpaces2 = "\t trimtext \t";
    protected string TaintedString = "TaintedString";

    public StringTrimTests()
    {
        AddTainted(taintedValue);
        AddTainted(taintedValueSpaces);
        AddTainted(taintedValueSpaces2);
        AddTainted(TaintedString);
    }

    //Trim()

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
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:", String.Concat(TaintedString, "   ").Trim(), () => String.Concat(TaintedString, "   ").Trim());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingTrim_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:", String.Concat(TaintedString, "\t\n\r").Trim(), () => String.Concat(TaintedString, "\t\n\r").Trim());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingTrim_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:", String.Concat("   ", TaintedString).Trim(), () => String.Concat("   ", TaintedString).Trim());
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimWithoutParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext-+:", taintedValueSpaces2.Trim(), () => (taintedValueSpaces2).Trim());
    }

    // System.String::Trim(System.Char[])

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimWithCharArrayParam_ResultIsTainted1()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext-+:", taintedValueSpaces2.Trim(new char[] { }), () => taintedValueSpaces2.Trim(new char[] { }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimWithCharArrayParam_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext-+:", taintedValueSpaces.Trim(new char[] { ' ' }), () => taintedValueSpaces.Trim(new char[] { ' ' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimWithCharArrayParam_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ainte-+:", taintedValue.Trim(new char[] { 't', 'd' }), () => taintedValue.Trim(new char[] { 't', 'd' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimWithCharArray_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-\t trimtext \t-+:", taintedValueSpaces2.Trim(new char[] { ' ' }), () => (taintedValueSpaces2).Trim(new char[] { ' ' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimCharArray_ResultIsTainted5()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+- trimtext -+:", taintedValueSpaces2.Trim(new char[] { '\t' }), () => (taintedValueSpaces2).Trim(new char[] { '\t' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimWithCharArrayParam_ResultIsTainted4()
    {
        var result = taintedValue.Trim(new char[] { 't', 'a', 'i', 'n', 't', 'e', 'd' });
        AssertNotTainted(result);
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimWithCharArrayParam_ResultIsTainted5()
    {
        AssertUntaintedWithOriginalCallCheck(String.Empty,
            taintedValue.Trim(new char[] { 't', 'a', 'i', 'n', 'e', 'd' }),
            () => taintedValue.Trim(new char[] { 't', 'a', 'i', 'n', 'e', 'd' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimWithNullParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext-+:", (taintedValueSpaces2).Trim(null), () => (taintedValueSpaces2).Trim(null));
    }

    // System.String::Trim(System.Char)

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimWithoutParams_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ainted-+:", taintedValue.Trim('t'), () => (taintedValue).Trim('t'));
    }

    // System.String::TrimStart(System.Char[])

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimStartWithNullParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext \t-+:", taintedValueSpaces2.TrimStart(null), () => (taintedValueSpaces2).TrimStart(null));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimStartCharArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext \t-+:", taintedValueSpaces2.TrimStart(new char[] { }), () => (taintedValueSpaces2).TrimStart(new char[] { }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimStartCharArray_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext -+:", taintedValueSpaces.TrimStart(new char[] { ' ' }), () => (taintedValueSpaces).TrimStart(new char[] { ' ' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimStartCharArray_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-rimtext -+:", taintedValueSpaces.TrimStart(new char[] { ' ', 't' }), () => (taintedValueSpaces).TrimStart(new char[] { ' ', 't' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimStartCharArray_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-\t trimtext \t-+:", taintedValueSpaces2.TrimStart(new char[] { ' ' }), () => (taintedValueSpaces2).TrimStart(new char[] { ' ' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimStartCharArray_ResultIsTainted5()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+- trimtext \t-+:", taintedValueSpaces2.TrimStart(new char[] { '\t' }), () => (taintedValueSpaces2).TrimStart(new char[] { '\t' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimStartWithCharArrayParam_ResultIsTainted6()
    {
        AssertUntaintedWithOriginalCallCheck(String.Empty,
            taintedValue.TrimStart(new char[] { 't', 'a', 'i', 'n', 'e', 'd' }),
            () => taintedValue.TrimStart(new char[] { 't', 'a', 'i', 'n', 'e', 'd' }));
    }

    // System.String::TrimStart(System.Char)

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimStart_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext -+:", taintedValueSpaces.TrimStart(' '), () => (taintedValueSpaces).TrimStart(' '));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimStart_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ainted-+:", taintedValue.TrimStart('t'), () => taintedValue.TrimStart('t'));
    }

    // System.String::TrimStart()

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimStartNoParameters_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-trimtext \t-+:", taintedValueSpaces2.TrimStart(), () => (taintedValueSpaces2).TrimStart());
    }

    // System.String::TrimEnd(System.Char[])

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimEndWithCharArrayParam_ResultIsTainted5()
    {
        AssertUntaintedWithOriginalCallCheck(String.Empty,
            taintedValue.TrimEnd(new char[] { 't', 'a', 'i', 'n', 'e', 'd' }),
            () => taintedValue.TrimEnd(new char[] { 't', 'a', 'i', 'n', 'e', 'd' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimEndCharArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-\t trimtext-+:", taintedValueSpaces2.TrimEnd(new char[] { }), () => (taintedValueSpaces2).TrimEnd(new char[] { }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimEndCharArray_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-\t trimtext \t-+:", taintedValueSpaces2.TrimEnd(new char[] { ' ' }), () => (taintedValueSpaces2).TrimEnd(new char[] { ' ' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimEndCharArray_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+- trimtex-+:", taintedValueSpaces.TrimEnd(new char[] { ' ', 't' }), () => (taintedValueSpaces).TrimEnd(new char[] { ' ', 't' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimEndCharArray_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-\t trimtext \t-+:", taintedValueSpaces2.TrimEnd(new char[] { ' ' }), () => (taintedValueSpaces2).TrimEnd(new char[] { ' ' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimEndCharArray_ResultIsTainted5()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-\t trimtext -+:", taintedValueSpaces2.TrimEnd(new char[] { '\t' }), () => (taintedValueSpaces2).TrimEnd(new char[] { '\t' }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimEndWithNullParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-\t trimtext-+:", taintedValueSpaces2.TrimEnd(null), () => (taintedValueSpaces2).TrimEnd(null));
    }

    // System.String::TrimEnd(System.Char)

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimEnd_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+- trimtext-+:", taintedValueSpaces.TrimEnd(' '), () => (taintedValueSpaces).TrimEnd(' '));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimEnd_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainte-+:", taintedValue.TrimEnd('d'), () => taintedValue.TrimEnd('d'));
    }

    // System.String::TrimEnd()

    [Fact]
    public void GivenATaintedObject_WhenCallingTrimEndNoParameters_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-\t trimtext-+:", taintedValueSpaces2.TrimEnd(), () => (taintedValueSpaces2).TrimEnd());
    }
}
