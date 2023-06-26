using System.Text;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringBuilderPropagation;
public class StringBuilderReplaceTests : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";
    private string _taintedValue2 = "TAINTED2";
    private string _untaintedString = "untaintedString";
    private string _notTaintedValue = "NotTainted";
    private string _taintedString = "TaintedString";

    public StringBuilderReplaceTests()
    {
        AddTainted(_taintedValue);
        AddTainted(_taintedValue2);
        AddTainted(_taintedString);
    }

    [Fact]
    public void StringBuilder_Replace_With_Tainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-Builder-+:", 
            GetTaintedStringBuilder("TaintedStringBuilder").Replace(_taintedString, string.Empty),
            () => GetTaintedStringBuilder("TaintedStringBuilder").Replace(_taintedString, string.Empty));
    }

    [Fact]
    public void StringBuilder_Replace_With_Untainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-untaintedStringBuilder-+:", 
            GetTaintedStringBuilder("TaintedStringBuilder").Replace(_taintedString, _untaintedString),
            () => GetTaintedStringBuilder("TaintedStringBuilder").Replace(_taintedString, _untaintedString));
    }

    [Fact]
    public void StringBuilder_Replace_Index_With_Tainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-LargeTaintedStringBuilder-+:",
            GetTaintedStringBuilder("TaintedStringBuilder").Replace(_taintedString, AddTainted("LargeTaintedString") as string, 0, 18),
            () => GetTaintedStringBuilder("TaintedStringBuilder").Replace(_taintedString, AddTainted("LargeTaintedString") as string, 0, 18));
    }

    [Fact]
    public void StringBuilder_Replace_Index_With_Untainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-untaintedStringBuilder-+:", 
            GetTaintedStringBuilder("TaintedStringBuilder").Replace(_taintedString, _untaintedString, 0, 13),
            () => GetTaintedStringBuilder("TaintedStringBuilder").Replace(_taintedString, _untaintedString, 0, 13));
    }

    [Fact]
    public void StringBuilder_Replace_Index_With_Untainted_Init()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-XaintedStringBuilder-+:", 
            GetTaintedStringBuilder("TaintedStringBuilder").Replace("T", "X"),
            () => GetTaintedStringBuilder("TaintedStringBuilder").Replace("T", "X"));
    }

    [Fact]
    public void StringBuilder_Replace_Index_With_Untainted_Mid()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedXtringBuilder-+:", 
            GetTaintedStringBuilder("TaintedStringBuilder").Replace("S", "X", 0, 16),
            () => GetTaintedStringBuilder("TaintedStringBuilder").Replace("S", "X", 0, 16));
    }

    [Fact]
    public void StringBuilder_Replace_Char_With_Tainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-XaintedStringBuilder-+:", 
            GetTaintedStringBuilder("TaintedStringBuilder").Replace('T', 'X'),
            () => GetTaintedStringBuilder("TaintedStringBuilder").Replace('T', 'X'));
    }

    [Fact]
    public void TestReplace()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringuntaintedStringuilder-+:",
            GetTaintedStringBuilder("TaintedStringBuilder").Replace("taintedString", "XX").Replace("B", _untaintedString).Replace("XX", _taintedString),
            () => GetTaintedStringBuilder("TaintedStringBuilder").Replace("taintedString", "XX").Replace("B", _untaintedString).Replace("XX", _taintedString));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceCharTainted_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TainTed-+:", 
            new StringBuilder(_taintedValue).Replace('t', 'T').ToString(), 
            () => new StringBuilder(_taintedValue).Replace('t', 'T').ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceCharTaintedIndexCount_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainTed-+:", 
            new StringBuilder(_taintedValue).Replace('t', 'T', 2, 5).ToString(), 
            () => new StringBuilder(_taintedValue).Replace('t', 'T', 2, 5).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceCharTaintedIndexCount_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-taintEd-+:", 
            new StringBuilder(_taintedValue).Replace('e', 'E', 2, 5).ToString(), 
            () => new StringBuilder(_taintedValue).Replace('e', 'E', 2, 5).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceCharTaintedIndexCount_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", 
            new StringBuilder(_taintedValue).Replace('e', 'E', 2, 0).ToString(), 
            () => new StringBuilder(_taintedValue).Replace('e', 'E', 2, 0).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceCharTaintedIndexCount_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-taintEd-+:", 
            new StringBuilder(_taintedValue).Replace('e', 'E').ToString(), 
            () => new StringBuilder(_taintedValue).Replace('e', 'E').ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceCharTaintedIndexCountWrongArguments_ArgumentOutOfRangeException()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Replace('t', 'T', -2, 2), 
            () => new StringBuilder(_taintedValue).Replace('t', 'T', -2, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceCharTaintedIndexCountWrongArguments_ArgumentOutOfRangeException2()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Replace('t', 'T', 200, 2), 
            () => new StringBuilder(_taintedValue).Replace('t', 'T', 200, 2));
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceCharTaintedIndexCountWrongArguments_ArgumentOutOfRangeException3()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Replace('t', 'T', 2, -2), 
            () => new StringBuilder(_taintedValue).Replace('t', 'T', 2, -2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringTainted_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TainTed-+:", 
            new StringBuilder(_taintedValue).Replace("t", "T").ToString(), 
            () => new StringBuilder(_taintedValue).Replace("t", "T").ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringTainted_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TTTainTTTed-+:", 
            new StringBuilder(_taintedValue).Replace("t", "TTT").ToString(), 
            () => new StringBuilder(_taintedValue).Replace("t", "TTT").ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringTainted_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-taiNted-+:", 
            new StringBuilder(_taintedValue).Replace("n", "N").ToString(), 
            () => new StringBuilder(_taintedValue).Replace("n", "N").ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringTaintedIndexCount_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainTed-+:", 
            new StringBuilder(_taintedValue).Replace("t", "T", 2, 5).ToString(), 
            () => new StringBuilder(_taintedValue).Replace("t", "T", 2, 5).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringTaintedIndexCount_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainTTTed-+:", 
            new StringBuilder(_taintedValue).Replace("t", "TTT", 2, 5).ToString(), 
            () => new StringBuilder(_taintedValue).Replace("t", "TTT", 2, 5).ToString());
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringTaintedIndexCountWrongArguments_ArgumentOutOfRangeException()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Replace("t", "T", -2, 2).ToString(), 
            () => new StringBuilder(_taintedValue).Replace("t", "T", -2, 2).ToString());
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringTaintedIndexCountWrongArguments_ArgumentOutOfRangeException2()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Replace("t", "T", 200, 2).ToString(), 
            () => new StringBuilder(_taintedValue).Replace("t", "T", 200, 2).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringNotTaintedWithTainted_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-NoTAINTED2TainTAINTED2ed-+:", 
            new StringBuilder(_notTaintedValue).Replace("t", _taintedValue2).ToString(), 
            () => new StringBuilder(_notTaintedValue).Replace("t", _taintedValue2).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringNotTaintedWithTaintedIndexCount_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-NoTAINTED2Tainted-+:", 
            new StringBuilder(_notTaintedValue).Replace("t", _taintedValue2, 2, 5).ToString(), 
            () => new StringBuilder(_notTaintedValue).Replace("t", _taintedValue2, 2, 5).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringNotTaintedWithTaintedPattern_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-NotTtttinted-+:",
            new StringBuilder(_notTaintedValue).Replace(_taintedValue.Substring(1, 1), "ttt").ToString(), 
            () => new StringBuilder(_notTaintedValue).Replace(_taintedValue.Substring(1, 1), "ttt").ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringNotTaintedWithNotTaintedPattern_ResultIsNotTainted()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_notTaintedValue).Replace(_notTaintedValue.Substring(1), "ttt").ToString(),
            () => new StringBuilder(_notTaintedValue).Replace(_notTaintedValue.Substring(1), "ttt").ToString());
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringTaintedIndexCountWrongArguments_ArgumentOutOfRangeException3()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Replace("t", "T", 2, -2), 
            () => new StringBuilder(_taintedValue).Replace("t", "T", 2, -2));
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentNullException))]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringTaintedIndexCountWrongArguments_ArgumentNullException()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Replace((string)null, "T", 2, 2), 
            () => new StringBuilder(_taintedValue).Replace((string)null, "T", 2, 2));
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringTaintedIndexCountWrongArguments_ArgumentOutOfRangeException5()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Replace("t", (string)null, 2, -2), 
            () => new StringBuilder(_taintedValue).Replace("t", (string)null, 2, -2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringTaintedIndexCountWrongArguments_ArgumentOutOfRangeException6()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ained-+:", 
            new StringBuilder(_taintedValue).Replace("t", (string)null).ToString(), 
            () => new StringBuilder(_taintedValue).Replace("t", (string)null).ToString());
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentNullException))]
    public void GivenATaintedString_WhenCallingStringBuilderReplaceStringTaintedIndexCountWrongArguments_ArgumentNullException2()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Replace((string)null, "T"), 
            () => new StringBuilder(_taintedValue).Replace((string)null, "T"));
    }
}
