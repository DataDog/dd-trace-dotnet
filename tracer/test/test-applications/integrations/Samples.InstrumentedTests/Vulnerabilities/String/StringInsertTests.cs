using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;

public class StringInsertTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string LargeTaintedString = "LargeTaintedString";
    protected string TaintedString = "TaintedString";
    protected string UntaintedString = "UntaintedString";
    protected string OtherTaintedString = "OtherTaintedString";

    public StringInsertTests()
    {
        AddTainted(taintedValue);
        AddTainted(TaintedString);
        AddTainted(LargeTaintedString);
        AddTainted(OtherTaintedString);
    }

    [Fact]
    public void String_Insert()
    {
        string testString1 = AddTaintedString("01234");
        string testString2 = AddTaintedString("abc");

        AssertTaintedFormatWithOriginalCallCheck(":+-01-+::+-abc-+::+-234-+:", testString1.Insert(2, testString2), () => testString1.Insert(2, testString2));
        AssertTaintedFormatWithOriginalCallCheck(":+-01-+:---:+-234-+:", testString1.Insert(2, "---"), () => testString1.Insert(2, "---"));
        AssertTaintedFormatWithOriginalCallCheck("--:+-abc-+:--------", "----------".Insert(2, testString2), () => "----------".Insert(2, testString2));
    }

    [Fact]
    public void String_Insert_Basic_With_Untainted()
    {
        int index = 2;
        AssertTaintedFormatWithOriginalCallCheck(":+-La-+:X:+-rgeTaintedString-+:", LargeTaintedString.Insert(index, "X"), () => LargeTaintedString.Insert(index, "X"));
    }

    [Fact]
    public void String_Insert_Index_With_Untainted()
    {
        int index = 2;
        AssertTaintedFormatWithOriginalCallCheck(":+-La-+:UntaintedString:+-rgeTaintedString-+:", LargeTaintedString.Insert(index, UntaintedString), () => LargeTaintedString.Insert(index, UntaintedString));
    }

    [Fact]
    public void String_Insert_Index_With_Tainted()
    {
        int index = 2;
        AssertTaintedFormatWithOriginalCallCheck(":+-La-+::+-TaintedString-+::+-rgeTaintedString-+:", LargeTaintedString.Insert(index, TaintedString), () => LargeTaintedString.Insert(index, TaintedString));
    }

    [Fact]
    public void String_Insert_Index_With_Both()
    {
        int index = 2;
        string str = LargeTaintedString.Insert(index, UntaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-La-+:UntaintedString:+-rge-+::+-TaintedString-+::+-TaintedString-+:", str.Insert(20, TaintedString), () => str.Insert(20, TaintedString));
    }

    [Fact]
    public void String_Insert_Index_Two_Tainted_With_Two_Untainted()
    {
        string str = String.Concat(LargeTaintedString, " and ", OtherTaintedString);
        str = str.Insert(2, UntaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-La-+:UntaintedString:+-rgeTaintedString-+: and UntaintedString:+-OtherTaintedString-+:", str.Insert(38, UntaintedString), () => str.Insert(38, UntaintedString));
    }

    [Fact]
    public void String_Insert_Index_Two_Tainted_With_Two_Tainted()
    {
        string str = String.Concat(LargeTaintedString, " and ", OtherTaintedString);
        str = str.Insert(5, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-Large-+::+-OtherTaintedString-+::+-TaintedString-+: and :+-Other-+::+-TaintedString-+::+-TaintedString-+:", str.Insert(46, TaintedString), () => str.Insert(46, TaintedString));
    }

    [Fact]
    public void String_Insert_Index_Two_Tainted_With_Both()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-Large-+::+-OtherTaintedString-+::+-TaintedString-+: and :+-Other-+:UntaintedString:+-TaintedString-+:",
            String.Concat(LargeTaintedString, " and ", OtherTaintedString).Insert(5, OtherTaintedString).Insert(46, UntaintedString),
            () => String.Concat(LargeTaintedString, " and ", OtherTaintedString).Insert(5, OtherTaintedString).Insert(46, UntaintedString));
    }

    [Fact]
    public void String_Insert_Index_Both_With_Two_Untainted()
    {
        string str = String.Concat(LargeTaintedString, " and ", UntaintedString);
        str = str.Insert(2, UntaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-La-+:UntaintedString:+-rgeTaintedString-+: and UntaintedStringUntaintedString", str.Insert(38, UntaintedString), () => str.Insert(38, UntaintedString));
    }

    [Fact]
    public void String_Insert_Index_Both_With_Two_Tainted()
    {
        string str = String.Concat(LargeTaintedString, " and ", UntaintedString);
        str = str.Insert(2, TaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-La-+::+-TaintedString-+::+-rgeTaintedString-+: and Un:+-OtherTaintedString-+:taintedString", str.Insert(38, OtherTaintedString), () => str.Insert(38, OtherTaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingInsert_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+:ww:+-nted-+:", taintedValue.Insert(3, "ww"), () => taintedValue.Insert(3, "ww"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingInsert_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", taintedValue.Insert(3, string.Empty), () => taintedValue.Insert(3, string.Empty));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingInsert_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", string.Empty.Insert(0, taintedValue), () => string.Empty.Insert(0, taintedValue));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingInsert_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck("Un:+-TaintedString-+:taintedString", UntaintedString.Insert(2, TaintedString), () => UntaintedString.Insert(2, TaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingInsert_ResultIsTainted5()
    {
        Assert.Throws<ArgumentNullException>(() => taintedValue.Insert(3, null));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingInsertNullString_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => taintedValue.Insert(-3, "e"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingInsertNullString_ArgumentOutOfRangeException2()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => taintedValue.Insert(333, "e"));
    }
}
