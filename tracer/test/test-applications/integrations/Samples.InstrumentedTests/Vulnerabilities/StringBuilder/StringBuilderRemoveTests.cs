using System.Text;
using Xunit;
namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringBuilderPropagation;

public class StringBuilderRemoveTests : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";
    //private StringBuilder _taintedStringBuilder = new StringBuilder("TaintedStringBuilder");
    private string _taintedString = "TaintedString";
    private string _untaintedString = "UntaintedString";

    public StringBuilderRemoveTests()
    {
        AddTainted(_taintedValue);
        AddTainted(_taintedString);
        //AddTainted(_taintedStringBuilder);
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted()
    {
        var check = new StringBuilder("TaintedStringBuilder");
        var tainted = new StringBuilder("TaintedStringBuilder");
        AddTainted(tainted);

        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedBuilder-+:", 
            tainted.Remove(7, 6), 
            () => check.Remove(7, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted2()
    {
        var check = new StringBuilder("TaintedStringBuilder");
        var tainted = new StringBuilder("TaintedStringBuilder");
        AddTainted(tainted);

        tainted.Append(_untaintedString);
        check.Append(_untaintedString);

        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedBuilder-+:UntaintedString", 
            tainted.Remove(7, 6), 
            () => check.Remove(7, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted3()
    {
        var check = new StringBuilder("TaintedStringBuilder");
        var tainted = new StringBuilder("TaintedStringBuilder");
        AddTainted(tainted);

        tainted.Append(_taintedString);
        check.Append(_taintedString);

        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedBuilder-+::+-TaintedString-+:", 
            tainted.Remove(7, 6), 
            () => check.Remove(7, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted4()
    {
        var check = new StringBuilder("TaintedStringBuilder");
        var tainted = new StringBuilder("TaintedStringBuilder");
        AddTainted(tainted);

        tainted.Append(_untaintedString).Append(_taintedString);
        check.Append(_untaintedString).Append(_taintedString);

        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+:Untainted:+-TaintedString-+:", 
            tainted.Remove(29, 6), 
            () => check.Remove(29, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted5()
    {
        var check = new StringBuilder("TaintedStringBuilder");
        var tainted = new StringBuilder("TaintedStringBuilder");
        AddTainted(tainted);

        tainted.Append(_untaintedString).Append(_taintedString);
        check.Append(_untaintedString).Append(_taintedString);

        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedBuilder-+:UntaintedString:+-TaintedString-+:", 
            tainted.Remove(7, 6), 
            () => check.Remove(7, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted6()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedBuilderUntaintedString and tainted literal equals to TaintedString-+:",
            GetTaintedStringBuilder("TaintedStringBuilder").Append(_untaintedString).AppendFormat(" and tainted literal equals to {0}", _taintedString).Remove(7, 6), 
            () => GetTaintedStringBuilder("TaintedStringBuilder").Append(_untaintedString).AppendFormat(" and tainted literal equals to {0}", _taintedString).Remove(7, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted7()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-StringBuilderUntaintedString and tainted literal equals to TaintedString-+:",
            GetTaintedStringBuilder("TaintedStringBuilder").Append(_untaintedString).AppendFormat(" and tainted literal equals to {0}", _taintedString).Remove(0, 7), 
            () => GetTaintedStringBuilder("TaintedStringBuilder").Append(_untaintedString).AppendFormat(" and tainted literal equals to {0}", _taintedString).Remove(0, 7));
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

    [Theory]
    [InlineData(30, 3)]
    [InlineData(-30, 3)]
    [InlineData(3, 30)]
    [InlineData(3, -3)]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTaintedWrongAnguments_ArgumentOutOfRangeException(int index1, int index2)
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Remove(index1, index2), 
            () => new StringBuilder(_taintedValue).Remove(index1, index2));
    }
}

