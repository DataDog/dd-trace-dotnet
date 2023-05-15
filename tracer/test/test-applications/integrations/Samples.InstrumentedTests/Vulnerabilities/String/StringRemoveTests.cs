using System;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;
public class StringRemoveTests : InstrumentationTestsBase
{
    protected string TaintedString = "TaintedString";
    protected string UntaintedString = "UntaintedString";
    protected string OtherTaintedString = "OtherTaintedString";

    public StringRemoveTests()
    {
        AddTainted(TaintedString);
        AddTainted(OtherTaintedString);
    }

    [Fact]
    public void GivenTaintedStrings_WhenRemove_ResultIsOk()
    {
        string testString1 = AddTaintedString("0123456789");

        AssertTaintedFormatWithOriginalCallCheck(":+-012-+:", testString1.Remove(3), () => testString1.Remove(3));
        AssertTaintedFormatWithOriginalCallCheck(":+-3456789-+:", testString1.Remove(0, 3), () => testString1.Remove(0, 3));
        AssertTaintedFormatWithOriginalCallCheck(":+-01256789-+:", testString1.Remove(3, 2), () => testString1.Remove(3, 2));

        string temp = AddTaintedString("34567");
        string testString2 = "abc" + temp + "ij";

        FormatTainted(testString2).Should().Be("abc:+-34567-+:ij");
        AssertTaintedFormatWithOriginalCallCheck("abc:+-34567-+:", testString2.Remove(8), () => testString2.Remove(8));
        AssertTaintedFormatWithOriginalCallCheck("abc:+-345-+:", testString2.Remove(6), () => testString2.Remove(6));
        AssertTaintedFormatWithOriginalCallCheck("abc:+-347-+:ij", testString2.Remove(5, 2), () => testString2.Remove(5, 2));
        AssertTaintedFormatWithOriginalCallCheck("a:+-567-+:ij", testString2.Remove(1, 4), () => testString2.Remove(1, 4));
        AssertTaintedFormatWithOriginalCallCheck("c:+-34567-+:ij", testString2.Remove(0, 2), () => testString2.Remove(0, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk1()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-Tainted-+:", TaintedString.Remove(7), () => TaintedString.Remove(7));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk2()
    {
        string str = String.Concat(TaintedString, UntaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:Untainted", str.Remove(22), () => str.Remove(22));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-Taintedg-+:", TaintedString.Remove(7, 5), () => TaintedString.Remove(7, 5));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk4()
    {
        string str = String.Concat(TaintedString, UntaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-Tainted-+:UntaintedString", str.Remove(7, 6), () => str.Remove(7, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk5()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-Tainted-+:String:+-OtherTaintedString-+:", str.Remove(7, 15), () => str.Remove(7, 15));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk6()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:Untainted:+-TaintedString-+:", str.Remove(22, 11), () => str.Remove(22, 11));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk7()
    {
        TaintedString.Remove(0).Should().BeEmpty();
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk8()
    {
        TaintedString.Remove(0, TaintedString.Length).Should().BeEmpty();
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk10()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-intedString-+:", TaintedString.Remove(0, 2), () => TaintedString.Remove(0, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk11()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStri-+:", TaintedString.Remove(11, 2), () => TaintedString.Remove(11, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk12()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaindString-+:UntaintedString:+-OtherTaintedString-+:", str.Remove(4, 2), () => str.Remove(4, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk13()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-Tain-+:aintedString:+-OtherTaintedString-+:", str.Remove(4, 12), () => str.Remove(4, 12));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk13_2()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-Tain-+:UntaintedString:+-OtherTaintedString-+:", str.Remove(4, 9), () => str.Remove(4, 9));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk14()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:taintedString:+-OtherTaintedString-+:", str.Remove(13, 2), () => str.Remove(13, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk15()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+::+-OtherTaintedString-+:", str.Remove(13, 15), () => str.Remove(13, 15));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk16()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+::+-TaintedString-+:", str.Remove(13, 20), () => str.Remove(13, 20));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk17()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:U:+-aintedString-+:", str.Remove(14, 20), () => str.Remove(14, 20));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk18()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:U:+-OtherTaintedString-+:", str.Remove(14, 14), () => str.Remove(14, 14));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk19()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:UaintedString:+-OtherTaintedString-+:", str.Remove(14, 2), () => str.Remove(14, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk20()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:UntaintedString:+-herTaintedString-+:", str.Remove(28, 2), () => str.Remove(28, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk21()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:UntaintedString", str.Remove(28, 18), () => str.Remove(28, 18));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk22()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:UntaintedString:+-OtrTaintedString-+:", str.Remove(30, 2), () => str.Remove(30, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk23()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:UntaintedString:+-Ottring-+:", str.Remove(30, 11), () => str.Remove(30, 11));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk24()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-Tai-+::+-TaintedString-+:", str.Remove(3, 30), () => str.Remove(3, 30));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk25()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:", TaintedString.Remove(7, 0), () => TaintedString.Remove(7, 0));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk26()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-Ta-+:", str.Remove(2), () => str.Remove(2));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk27()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:Un", str.Remove(15), () => str.Remove(15));
    }

    [Fact]
    public void GivenATaintedString_WhenRemove_ResultIsOk28()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:UntaintedString:+-Ot-+:", str.Remove(30), () => str.Remove(30));
    }

    [Fact]
    public void GivenATaintedString_WhenRemoveIncrrectValues_ExceptionIsThrown1()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TaintedString.Remove(-1));
    }

    [Fact]
    public void GivenATaintedString_WhenRemoveIncrrectValues_ExceptionIsThrown2()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TaintedString.Remove(33));
    }

    [Fact]
    public void GivenATaintedString_WhenRemoveIncrrectValues_ExceptionIsThrown3()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TaintedString.Remove(2, 333));
    }

    [Fact]
    public void GivenATaintedString_WhenRemoveIncrrectValues_ExceptionIsThrown4()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TaintedString.Remove(2, -1));
    }
}
