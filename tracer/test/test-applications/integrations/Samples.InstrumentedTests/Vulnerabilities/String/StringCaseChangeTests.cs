using System;
using System.Globalization;
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
        string txt = AddTaintedString("0a2");
        AssertTaintedFormatWithOriginalCallCheck(":+-0A2-+:", txt.ToUpper(), () => "0a2".ToUpper());
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToUpper_ResultIsOk()
    {
        string testString1 = AddTaintedString("01234");
        AssertTaintedFormatWithOriginalCallCheck(":+-01234-+:", testString1.ToUpper(), () => testString1.ToUpper());

        string testString2 = AddTaintedString("abcde");
        AssertTaintedFormatWithOriginalCallCheck(":+-ABCDE-+:", testString2.ToUpper(), () => testString2.ToUpper());

        string str1 = AddTaintedString("0a2");
        string str2 = AddTaintedString("0b2");
        AssertTaintedFormatWithOriginalCallCheck("    :+-0A2-+:    :+-0B2-+:",
            String.Concat("    ", str1, "    ", str2).ToUpper(),
            () => String.Concat("    ", str1, "    ", str2).ToUpper());
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToLower_ResultIsOk()
    {
        string testString1 = AddTaintedString("01234");
        AssertTaintedFormatWithOriginalCallCheck(":+-01234-+:", testString1.ToLower(), () => testString1.ToLower());

        string testString2 = AddTaintedString("ABCDE");
        AssertTaintedFormatWithOriginalCallCheck(":+-abcde-+:", testString2.ToLower(), () => testString2.ToLower());

        string str1 = AddTaintedString("0A2");
        string str2 = AddTaintedString("0B2");
        AssertTaintedFormatWithOriginalCallCheck("    :+-0a2-+:    :+-0b2-+:",
            String.Concat("    ", str1, "    ", str2).ToLower(),
            () => String.Concat("    ", str1, "    ", str2).ToLower());
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToUpperInvariant_ResultIsOk()
    {
        string testString1 = AddTaintedString("01234");
        AssertTaintedFormatWithOriginalCallCheck(":+-01234-+:", testString1.ToUpperInvariant(), () => testString1.ToUpperInvariant());

        string testString2 = AddTaintedString("abcde");
        AssertTaintedFormatWithOriginalCallCheck(":+-ABCDE-+:", testString2.ToUpperInvariant(), () => testString2.ToUpperInvariant());

        string str1 = AddTaintedString("0a2");
        string str2 = AddTaintedString("0b2");
        AssertTaintedFormatWithOriginalCallCheck("    :+-0A2-+:    :+-0B2-+:",
            String.Concat("    ", str1, "    ", str2).ToUpperInvariant(),
            () => String.Concat("    ", str1, "    ", str2).ToUpperInvariant());
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingToLowerInvariant_ResultIsOk()
    {
        string testString1 = AddTaintedString("01234");
        AssertTaintedFormatWithOriginalCallCheck(":+-01234-+:", testString1.ToLowerInvariant(), () => testString1.ToLowerInvariant());

        string testString2 = AddTaintedString("ABCDE");
        AssertTaintedFormatWithOriginalCallCheck(":+-abcde-+:", testString2.ToLowerInvariant(), () => testString2.ToLowerInvariant());

        string str1 = AddTaintedString("0A2");
        string str2 = AddTaintedString("0B2");
        AssertTaintedFormatWithOriginalCallCheck("    :+-0a2-+:    :+-0b2-+:", 
            String.Concat("    ", str1, "    ", str2).ToLowerInvariant(), 
            () => String.Concat("    ", str1, "    ", str2).ToLowerInvariant());
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

    [Fact]
    public void GivenATaintedObject_WhenCallingToUpperWithTurkishCulture_ResultIsTainted()
    {
        CultureInfo turkishCulture = CultureInfo.GetCultureInfo("tr-TR");
        var tainted = AddTaintedString("turkish i");

        tainted.ToUpper(turkishCulture).Should().NotBe(tainted.ToUpper(CultureInfo.InvariantCulture));
        AssertTaintedFormatWithOriginalCallCheck(":+-TURKİSH İ-+:", tainted.ToUpper(turkishCulture), () => tainted.ToUpper(turkishCulture));
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
    public void GivenATaintedObject_WhenCallingToLowerWithTurkishCulture_ResultIsTainted()
    {
        CultureInfo turkishCulture = CultureInfo.GetCultureInfo("tr-TR");
        var tainted = AddTaintedString("TURKİSH İ");

        tainted.ToLower(turkishCulture).Should().NotBe(tainted.ToLower(CultureInfo.InvariantCulture));
        AssertTaintedFormatWithOriginalCallCheck(":+-turkish i-+:", tainted.ToLower(turkishCulture), () => tainted.ToLower(turkishCulture));
    }
    [Fact]
    public void GivenATaintedObject_WhenCallingToLowerInvariant_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", taintedValue.ToLowerInvariant(),
            () => taintedValue.ToLowerInvariant());
    }

}
