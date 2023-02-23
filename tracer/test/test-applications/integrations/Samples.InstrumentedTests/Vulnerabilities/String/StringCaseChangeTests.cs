using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;

public class StringCaseChangeTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";

    public StringCaseChangeTests()
    {
        AddTainted(taintedValue);
    }

    [Fact]
    public void GivenANotTaintedObject_WhenCallingToUpper_ResultIsOk()
    {
        string txt = "0a2";
        string txt1 = txt.ToUpper();
        string txt2 = "0a2".ToUpper();
        Assert.Equal(txt1, txt2);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToUpper_ResultIsOk()
    {
        string testString1 = AddTaintedString("01234");
        Assert.Equal(":+-01234-+:", FormatTainted(testString1.ToUpper()));

        string testString2 = AddTaintedString("abcde");
        Assert.Equal(":+-ABCDE-+:", FormatTainted(testString2.ToUpper()));

        string str1 = AddTaintedString("0a2");
        string str2 = AddTaintedString("0b2");
        string testString = String.Concat("    ", str1, "    ", str2);

        Assert.Equal("    :+-0A2-+:    :+-0B2-+:", FormatTainted(testString.ToUpper()));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToLower_ResultIsOk()
    {
        string testString1 = AddTaintedString("01234");
        Assert.Equal(":+-01234-+:", FormatTainted(testString1.ToLower()));

        string testString2 = AddTaintedString("ABCDE");
        Assert.Equal(":+-abcde-+:", FormatTainted(testString2.ToLower()));

        string str1 = AddTaintedString("0A2");
        string str2 = AddTaintedString("0B2");
        string testString = String.Concat("    ", str1, "    ", str2);

        Assert.Equal("    :+-0a2-+:    :+-0b2-+:", FormatTainted(testString.ToLower()));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToUpperInvariant_ResultIsOk()
    {
        string testString1 = AddTaintedString("01234");
        Assert.Equal(":+-01234-+:", FormatTainted(testString1.ToUpperInvariant()));

        string testString2 = AddTaintedString("abcde");
        Assert.Equal(":+-ABCDE-+:", FormatTainted(testString2.ToUpperInvariant()));

        string str1 = AddTaintedString("0a2");
        string str2 = AddTaintedString("0b2");
        string testString = String.Concat("    ", str1, "    ", str2);

        Assert.Equal("    :+-0A2-+:    :+-0B2-+:", FormatTainted(testString.ToUpperInvariant()));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToLowerInvariant_ResultIsOk()
    {
        string testString1 = AddTaintedString("01234");
        Assert.Equal(":+-01234-+:", FormatTainted(testString1.ToLowerInvariant()));

        string testString2 = AddTaintedString("ABCDE");
        Assert.Equal(":+-abcde-+:", FormatTainted(testString2.ToLowerInvariant()));

        string str1 = AddTaintedString("0A2");
        string str2 = AddTaintedString("0B2");
        string testString = String.Concat("    ", str1, "    ", str2);

        Assert.Equal("    :+-0a2-+:    :+-0b2-+:", FormatTainted(testString.ToLowerInvariant()));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToUpperWithoutParameters_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED-+:", taintedValue.ToUpper(), () => taintedValue.ToUpper());
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToUpperWithCulture_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED-+:", taintedValue.ToUpper(CultureInfo.InvariantCulture), () => taintedValue.ToUpper(CultureInfo.InvariantCulture));
    }

#if NETFRAMEWORK || NETCOREAPP2_1
    [Fact]
    public void GivenATaintedObject_WhenCallingToUpperWithNullCulture_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => taintedValue.ToUpper(null));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToLowerWithNullCulture_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => taintedValue.ToLower(null));
    }
#else
    [Fact]
    public void GivenATaintedObject_WhenCallingToUpperWithNullCulture_ResultIsOk()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED-+:", taintedValue.ToUpper(null), () => taintedValue.ToUpper(null));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToLowerWithNullCulture_ResultIsOk()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", taintedValue.ToLower(null), () => taintedValue.ToLower(null));
    }

#endif

    [Fact]
    public void GivenATaintedObject_WhenCallingToUpperInvariant_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED-+:", taintedValue.ToUpperInvariant(),
            () => taintedValue.ToUpperInvariant());
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToLowerWithoutParameters_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", taintedValue.ToLower(), () => taintedValue.ToLower());
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToLowerWithCulture_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", taintedValue.ToLower(CultureInfo.InvariantCulture), () => taintedValue.ToLower(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToLowerInvariant_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", taintedValue.ToLowerInvariant(),
            () => taintedValue.ToLowerInvariant());
    }

}
