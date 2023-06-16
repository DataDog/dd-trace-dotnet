using System;
using System.Text;
using Xunit;
namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringBuilderPropagation;

public class StringBuilderRemoveTests : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";
    private StringBuilder TaintedStringBuilder = new StringBuilder("TaintedStringBuilder");
    private string TaintedString = "TaintedString";
    private string UntaintedString = "UntaintedString";

    public StringBuilderRemoveTests()
    {
        AddTainted(_taintedValue);
        AddTainted(TaintedString);
        AddTainted(TaintedStringBuilder);
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedBuilder-+:", 
            TaintedStringBuilder.Remove(7, 6), 
            () => TaintedStringBuilder.Remove(7, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted2()
    {
        StringBuilder strb = TaintedStringBuilder.Append(UntaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedBuilder-+:UntaintedString", 
            strb.Remove(7, 6), 
            () => strb.Remove(7, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted3()
    {
        StringBuilder strb = TaintedStringBuilder.Append(TaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedBuilder-+::+-TaintedString-+:", 
            strb.Remove(7, 6), 
            () => strb.Remove(7, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted4()
    {
        StringBuilder strb = TaintedStringBuilder.Append(UntaintedString);
        strb = strb.Append(TaintedString);

        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+:Untainted:+-TaintedString-+:", 
            strb.Remove(29, 6), 
            () => strb.Remove(29, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted5()
    {
        StringBuilder strb = TaintedStringBuilder.Append(UntaintedString);
        strb = strb.Append(TaintedString);

        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedBuilder-+:UntaintedString:+-TaintedString-+:", 
            strb.Remove(7, 6), 
            () => strb.Remove(7, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted6()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedBuilderUntaintedString and tainted literal equals to TaintedString-+:",
            GetTaintedStringBuilder("TaintedStringBuilder").Append(UntaintedString).AppendFormat(" and tainted literal equals to {0}", TaintedString).Remove(7, 6), 
            () => GetTaintedStringBuilder("TaintedStringBuilder").Append(UntaintedString).AppendFormat(" and tainted literal equals to {0}", TaintedString).Remove(7, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted7()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-StringBuilderUntaintedString and tainted literal equals to TaintedString-+:",
            GetTaintedStringBuilder("TaintedStringBuilder").Append(UntaintedString).AppendFormat(" and tainted literal equals to {0}", TaintedString).Remove(0, 7), 
            () => GetTaintedStringBuilder("TaintedStringBuilder").Append(UntaintedString).AppendFormat(" and tainted literal equals to {0}", TaintedString).Remove(0, 7));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted8()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-taid-+:", 
            new StringBuilder(_taintedValue).Remove(3, 3).ToString(), 
            () => new StringBuilder(_taintedValue).Remove(3, 3).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted9()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-taid-+:abc", 
            new StringBuilder(_taintedValue).Append("abc").Remove(3, 3).ToString(), 
            () => new StringBuilder(_taintedValue).Append("abc").Remove(3, 3).ToString());
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTaintedWrongAnguments_ArgumentOutOfRangeException()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Remove(30, 3), 
            () => new StringBuilder(_taintedValue).Remove(30, 3));
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTaintedWrongAnguments_ArgumentOutOfRangeException2()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Remove(-30, 3), 
            () => new StringBuilder(_taintedValue).Remove(-30, 3));
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTaintedWrongAnguments_ArgumentOutOfRangeException3()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Remove(3, 30), 
            () => new StringBuilder(_taintedValue).Remove(3, 30));
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTaintedWrongAnguments_ArgumentOutOfRangeException4()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Remove(3, -3), 
            () => new StringBuilder(_taintedValue).Remove(3, -3));
    }
}

