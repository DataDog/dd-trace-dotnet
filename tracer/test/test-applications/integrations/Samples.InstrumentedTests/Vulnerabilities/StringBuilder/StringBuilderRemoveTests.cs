using System.Text;
using Xunit;
namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringBuilderPropagation;

public class StringBuilderRemoveTests : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";
    private StringBuilder _taintedStringBuilder = new StringBuilder("TaintedStringBuilder");
    private string _taintedString = "TaintedString";
    private string _untaintedString = "UntaintedString";

    public StringBuilderRemoveTests()
    {
        AddTainted(_taintedValue);
        AddTainted(_taintedString);
        AddTainted(_taintedStringBuilder);
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedBuilder-+:", 
            _taintedStringBuilder.Remove(7, 6), 
            () => _taintedStringBuilder.Remove(7, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted2()
    {
        StringBuilder strb = _taintedStringBuilder.Append(_untaintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedBuilder-+:UntaintedString", 
            strb.Remove(7, 6), 
            () => strb.Remove(7, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted3()
    {
        StringBuilder strb = _taintedStringBuilder.Append(_taintedString);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedBuilder-+::+-TaintedString-+:", 
            strb.Remove(7, 6), 
            () => strb.Remove(7, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted4()
    {
        StringBuilder strb = _taintedStringBuilder.Append(_untaintedString);
        strb = strb.Append(_taintedString);

        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+:Untainted:+-TaintedString-+:", 
            strb.Remove(29, 6), 
            () => strb.Remove(29, 6));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderRemoveTainted_ResultIsTainted5()
    {
        StringBuilder strb = _taintedStringBuilder.Append(_untaintedString);
        strb = strb.Append(_taintedString);

        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedBuilder-+:UntaintedString:+-TaintedString-+:", 
            strb.Remove(7, 6), 
            () => strb.Remove(7, 6));
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

