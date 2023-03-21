using System;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;

public class StringPadTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string TaintedString = "TaintedString";
    protected string UntaintedString = "UntaintedString";

    public StringPadTests()
    {
        AddTainted(taintedValue);
        AddTainted(TaintedString);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadLeft_ResultIsTainted()
    {
        string testString1 = AddTaintedString("abcd");
        AssertTaintedFormatWithOriginalCallCheck("                :+-abcd-+:", testString1.PadLeft(20), () => testString1.PadLeft(20));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadLeft_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck("     :+-TaintedString-+:", TaintedString.PadLeft(18), () => TaintedString.PadLeft(18));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadLeft_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck("    :+-TaintedString-+:UntaintedString", 
            String.Concat(TaintedString, UntaintedString).PadLeft(32)
            , () => String.Concat(TaintedString, UntaintedString).PadLeft(32));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadLeft_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck("*****:+-TaintedString-+:", TaintedString.PadLeft(18, '*'), () => TaintedString.PadLeft(18, '*'));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadLeft_ResultIsTainted5()
    {
        AssertTaintedFormatWithOriginalCallCheck("****:+-TaintedString-+:UntaintedString",
            String.Concat(TaintedString, UntaintedString).PadLeft(32, '*'), 
            () => String.Concat(TaintedString, UntaintedString).PadLeft(32, '*'));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadLeft_ResultIsTainted6()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:UntaintedString",
            String.Concat(TaintedString, UntaintedString).PadLeft(2, '*'),
            () => String.Concat(TaintedString, UntaintedString).PadLeft(2, '*'));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadLeft_ResultIsTainted7()
    {
        AssertTaintedFormatWithOriginalCallCheck("...:+-tainted-+:", taintedValue.PadLeft(10, '.'), () => taintedValue.PadLeft(10, '.'));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadLeft_ResultIsTainted8()
    {
        AssertTaintedFormatWithOriginalCallCheck("   :+-tainted-+:", taintedValue.PadLeft(10), () => taintedValue.PadLeft(10));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadLeftWrongIndex_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => taintedValue.PadLeft(-10));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadRight_ResultIsTainted()
    {
        string testString1 = AddTaintedString("abcd");
        AssertTaintedFormatWithOriginalCallCheck(":+-abcd-+:                ", testString1.PadRight(20), () => testString1.PadRight(20));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadRight_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:     ", TaintedString.PadRight(18), () => TaintedString.PadRight(18));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadRight_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:UntaintedString    ",
            String.Concat(TaintedString, UntaintedString).PadRight(32), 
            () => String.Concat(TaintedString, UntaintedString).PadRight(32));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadRight_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:*****", TaintedString.PadRight(18, '*'), () => TaintedString.PadRight(18, '*'));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadRight_ResultIsTainted5()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:UntaintedString****",
            String.Concat(TaintedString, UntaintedString).PadRight(32, '*'),
            () => String.Concat(TaintedString, UntaintedString).PadRight(32, '*'));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadRight_ResultIsTainted6()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:...", taintedValue.PadRight(10, '.'), () => taintedValue.PadRight(10, '.'));
    }

    [Fact]

    public void GivenATaintedObject_WhenCallingPadRight_ResultIsTainted7()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:   ", taintedValue.PadRight(10), () => taintedValue.PadRight(10));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadRight_ResultIsTainted8()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:UntaintedString",
            String.Concat(TaintedString, UntaintedString).PadRight(2, '*'),
            () => String.Concat(TaintedString, UntaintedString).PadRight(2, '*'));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingPadRightWrongIndex_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => taintedValue.PadRight(-10));
    }
}
