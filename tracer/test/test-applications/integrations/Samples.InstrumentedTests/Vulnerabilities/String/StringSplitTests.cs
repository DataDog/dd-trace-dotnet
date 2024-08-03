using System;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;
public class StringSplitTests : InstrumentationTestsBase
{
    private string _taintedSeparator = "i";
    private string _taintedValue = "tainted";
    private string _taintedString = "_taintedString";
    private string _otherTaintedString = "_otherTaintedString";
    private string _untaintedString = "untainted";
    private string _otherUntaintedString = "OtherUntaintedString";
    private static string ComposedTaintedString = "One|Two|Three";

    public StringSplitTests()
    {
        AddTaintedString(_taintedSeparator);
        AddTaintedString(_taintedValue);
        AddTaintedString(_taintedString);
        AddTaintedString(_otherTaintedString);
        AddTaintedString(ComposedTaintedString);
    }

    // Test System.String::Split(System.Char[])

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithChar_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ta-+:", _taintedValue.Split(new Char[] { 'i' })[0], () => _taintedValue.Split(new Char[] { 'i' })[0]);
        AssertTaintedFormatWithOriginalCallCheck(":+-nted-+:", _taintedValue.Split(new Char[] { 'i' })[1], () => _taintedValue.Split(new Char[] { 'i' })[1]);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithChar_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", _taintedValue.Split(new Char[] { 'x' })[0], () => _taintedValue.Split(new Char[] { 'x' })[0]);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithNullChar_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", _taintedValue.Split(null)[0], () => _taintedValue.Split(null)[0]);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithChar_ResultIsNotTainted2()
    {
        AssertNoneTainted(_untaintedString.Split('i'));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithChar_ResultIsTainted4()
    {
        var expected = new string[] { "One", "", "Two", "Three" };
        var target = AddTaintedString("One,,") + "Two" + AddTaintedString("|Three");
        var results = target.Split(new char[] { '|', ',' });

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    // Test System.String::Split(System.Char[],System.Int32)

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharAndIndex_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ta-+:", _taintedValue.Split(new Char[] { 'i' }, 2)[0], () => _taintedValue.Split(new Char[] { 'i' }, 2)[0]);
        AssertTaintedFormatWithOriginalCallCheck(":+-nted-+:", _taintedValue.Split(new Char[] { 'i' }, 2)[1], () => _taintedValue.Split(new Char[] { 'i' }, 2)[1]);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharAndIndex_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", _taintedValue.Split(new Char[] { 'W' }, 2)[0], () => _taintedValue.Split(new Char[] { 'W' }, 2)[0]);
    }

    [Fact]
    // ExpectedException System.ArgumentOutOfRangeException
    public void GivenATaintedObject_WhenCallingSplitWithWrongLimit_ArgumentOutOfRangeException()
    {
        AssertUntaintedWithOriginalCallCheck(() => _taintedValue.Split(new char[] { 'i' }, -2), () => _taintedValue.Split(new char[] { 'i' }, -2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharAndIndex_ResultIsTainted3()
    {
        var str = String.Concat(_taintedString, "|", _untaintedString);
        var expected = new string[] { _taintedString, _untaintedString };

        AssertEqual(expected, str.Split(new char[] { '|' }, 2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharAndIndex_ResultIsTainted4()
    {
        var str = String.Concat(_taintedString, "|", _untaintedString, "|", _otherTaintedString, "|", _otherUntaintedString);
        var expected = new string[] { _taintedString, _untaintedString, _otherTaintedString + "|" + _otherUntaintedString };

        var result = str.Split(new char[] { '|' }, 3);
        AssertEqual(expected, result);
        AssertAllTainted(result);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharAndIndex_ResultIsTainted5()
    {
        var expected = new string[] { "One", "Two|Three" };
        var results = ComposedTaintedString.Split(new char[] { '|' }, 2);

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]

    public void GivenATaintedObject_WhenCallingSplitWithCharAndIndex_ResultIsTainted6()
    {
        var expected = new string[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight|Nine" };
        var results = AddTaintedString("One|Two|Three|Four|Five|Six|Seven|Eight|Nine").Split(new char[] { '|' }, 8);

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]

    public void GivenATaintedObject_WhenCallingSplitWithCharAndIndex_ResultIsNotTainted()
    {
        var expected = new string[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight|Nine" };
        var results = "One|Two|Three|Four|Five|Six|Seven|Eight|Nine".Split(new char[] { '|' }, 8);

        AssertEqual(expected, results);
        AssertNoneTainted(results);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharArray_ResultIsNotTainted2()
    {
        AssertNoneTainted(_untaintedString.Split(new char[] { 'i' }));
    }

    // Test System.String::Split(System.Char[],System.StringSplitOptions)

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharArrayAndOptions_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ta-+:", _taintedValue.Split(new char[] { 'i' }, StringSplitOptions.None)[0], () => _taintedValue.Split(new char[] { 'i' }, StringSplitOptions.None)[0]);
        AssertTaintedFormatWithOriginalCallCheck(":+-nted-+:", _taintedValue.Split(new char[] { 'i' }, StringSplitOptions.None)[1], () => _taintedValue.Split(new char[] { 'i' }, StringSplitOptions.None)[1]);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharArrayAndOptions_ResultIsTainted2()
    {
        var str = String.Concat(_taintedString, "|", _untaintedString);
        var expected = new string[] { _taintedString, _untaintedString };

        var result = str.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        AssertEqual(expected, result);
        AssertAllTainted(result);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharArrayAndOptions_ResultIsTainted3()
    {
        var expected = new string[] { "One", "Two", "Three" };
        var results = ComposedTaintedString.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharArrayAndOptions_ResultIsTainted4()
    {
        var str = String.Concat(_taintedString, "|", _untaintedString, "|", _otherTaintedString, "|", _otherUntaintedString);
        var expected = new string[] { _taintedString, _untaintedString, _otherTaintedString, _otherUntaintedString };

        AssertEqual(expected, str.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharArrayAndOptions_ResultIsTainted5()
    {
        var expected = new string[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine" };
        var results = AddTaintedString("One|Two|Three|Four|Five|Six|Seven|Eight|Nine").Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharArrayAndOptions_ResultIsTainted6()
    {
        var expected = new string[] { "One", "Two", "Three" };
        var target = "One,," + AddTaintedString("Two|Three");
        var results = target.Split(new char[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharAndOptions_ResultIsTainted7()
    {
        var expected = new string[] { "One", "Two", "Three" };
        var target = "One,," + AddTaintedString("Two|Th") + "ree";
        var results = target.Split(new char[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharAndOptions_ResultIsTainted8()
    {
        var expected = new string[] { "One", "Two", "Three" };
        var target = "One,," + AddTaintedString("Two|") + "Three";
        var results = target.Split(new char[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    // Test System.String::Split(System.Char[],System.Int32,System.StringSplitOptions)

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharArrayCountAndOptions_ResultIsTainted()
    {
        var str = String.Concat(_taintedString, "|", _untaintedString);
        var expected = new string[] { _taintedString, _untaintedString };
        var results = str.Split(new char[] { '|' }, 2, StringSplitOptions.RemoveEmptyEntries);
        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharArrayCountAndOptions_ResultIsTainted2()
    {
        var str = String.Concat(_taintedString, "|", _untaintedString, "|", _otherTaintedString, "|", _otherUntaintedString);
        var expected = new string[] { _taintedString, _untaintedString, _otherTaintedString + "|" + _otherUntaintedString };
        var results = str.Split(new char[] { '|' }, 3, StringSplitOptions.RemoveEmptyEntries);
        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharArrayCountAndOptions_ResultIsTainted3()
    {
        var expected = new string[] { "One", "Two|Three" };
        var results = ComposedTaintedString.Split(new char[] { '|' }, 2, StringSplitOptions.RemoveEmptyEntries);
        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharArrayCountAndOptions_ResultIsTainted4()
    {
        var expected = new string[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight|Nine" };
        var results = AddTaintedString("One|Two|Three|Four|Five|Six|Seven|Eight|Nine").Split(new char[] { '|' }, 8, StringSplitOptions.RemoveEmptyEntries);

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharArrayCountAndOptions_ResultIsTainted6()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ta-+:", _taintedValue.Split(new char[] { 'i' }, 2, StringSplitOptions.None)[0], () => _taintedValue.Split(new char[] { 'i' }, 2, StringSplitOptions.None)[0]);
        AssertTaintedFormatWithOriginalCallCheck(":+-nted-+:", _taintedValue.Split(new char[] { 'i' }, 2, StringSplitOptions.None)[1], () => _taintedValue.Split(new char[] { 'i' }, 2, StringSplitOptions.None)[1]);
    }

    // Test System.String::Split(System.String[],System.StringSplitOptions)

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithStringAndOptions_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ta-+:", _taintedValue.Split(new string[] { "i" }, StringSplitOptions.None)[0], () => _taintedValue.Split(new string[] { "i" }, StringSplitOptions.None)[0]);
        AssertTaintedFormatWithOriginalCallCheck(":+-nted-+:", _taintedValue.Split(new string[] { "i" }, StringSplitOptions.None)[1], () => _taintedValue.Split(new string[] { "i" }, StringSplitOptions.None)[1]);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithStringArray_ResultIsNotTainted()
    {
        AssertNoneTainted(_untaintedString.Split(new string[] { _taintedSeparator }, StringSplitOptions.None));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithStringArray_ResultIsNotTainted2()
    {
        AssertNoneTainted(_untaintedString.Split(new string[] { "i" }, StringSplitOptions.None));
    }

    [Fact]
    public void String_Split_With_Tainted_WithStartRangeAndEndRangeAndSplitByRangeText()
    {
        var expected = new string[] { "One", "", "Two", "", "" };
        var target = AddTaintedString("One,,") + "Two" + AddTaintedString("|Three");
        var results = target.Split(new string[] { "|", ",", "Three" }, StringSplitOptions.None);

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]
    public void String_Split_With_Tainted_WithMiddleRangeAndSplitByRangeText2()
    {
        var expected = new string[] { "One", "Two", "Three" };
        var target = "On" + AddTaintedString("e|Tw") + "o|Three";
        var results = target.Split(new string[] { "|" }, StringSplitOptions.None);

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]
    public void String_Split_With_Tainted_WithMiddleRangeAndSplitByRangeText3()
    {
        var expected = new string[] { "One", "Two", "Three" };
        var target = "On" + AddTaintedString("e") + "|Tw" + AddTaintedString("o|Three");
        var results = target.Split(new string[] { "|" }, StringSplitOptions.None);

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]
    public void String_Split_With_Tainted_WithMiddleRangeAndSplitByRangeText4()
    {
        var expected = new string[] { "One", "Two", "Three" };
        var target = AddTaintedString("One") + AddTaintedString("|") + "|Tw" + AddTaintedString("o|Three");
        var results = target.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    // Test System.String::Split(System.String[],System.Int32,System.StringSplitOptions)

    [Fact]
    // ExpectedException System.ArgumentOutOfRangeException
    public void GivenATaintedObject_WhenCallingSplitWithWrongLimit_ArgumentOutOfRangeException2()
    {
        AssertUntaintedWithOriginalCallCheck(() => _taintedValue.Split(new string[] { "i" }, -2, StringSplitOptions.None), () => _taintedValue.Split(new string[] { "i" }, -2, StringSplitOptions.None));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithStringCountAndOptions_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", _taintedValue.Split(new string[] { "X" }, 1, StringSplitOptions.None)[0], () => _taintedValue.Split(new string[] { "X" }, 1, StringSplitOptions.None)[0]);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharArrayCountAndOptions_ResultIsTainted5()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ta-+:", _taintedValue.Split(new string[] { "i" }, 2, StringSplitOptions.None)[0], () => _taintedValue.Split(new string[] { "i" }, 2, StringSplitOptions.None)[0]);
        AssertTaintedFormatWithOriginalCallCheck(":+-nted-+:", _taintedValue.Split(new string[] { "i" }, 2, StringSplitOptions.None)[1], () => _taintedValue.Split(new string[] { "i" }, 2, StringSplitOptions.None)[1]);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithStringArrayCountOptions_ResultIsNotTainted()
    {
        AssertNoneTainted(_untaintedString.Split(new string[] { _taintedSeparator }, 1, StringSplitOptions.None));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithStringArrayCountOptions_ResultIsNotTainted2()
    {
        AssertNoneTainted(_untaintedString.Split(new string[] { "i" }, 1, StringSplitOptions.None));
    }

    // Test System.String::Split(System.String,System.Int32,System.StringSplitOptions)

#if NETCOREAPP3_1_OR_GREATER
    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithStringAndOptions_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ta-+:", _taintedValue.Split("in", 2, StringSplitOptions.None)[0], () => _taintedValue.Split("in", StringSplitOptions.None)[0]);
        AssertTaintedFormatWithOriginalCallCheck(":+-ted-+:", _taintedValue.Split("in", 2, StringSplitOptions.None)[1], () => _taintedValue.Split("in", StringSplitOptions.None)[1]);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithString_ResultIsNotTainted()
    {
        AssertNoneTainted(_untaintedString.Split(_taintedSeparator, StringSplitOptions.None));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithString_ResultIsNotTainted2()
    {
        AssertNoneTainted(_untaintedString.Split("i", StringSplitOptions.None));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithStringAndOptions_ResultIsTainted1()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ta-+:", _taintedValue.Split("in", StringSplitOptions.None)[0], () => _taintedValue.Split("in", StringSplitOptions.None)[0]);
        AssertTaintedFormatWithOriginalCallCheck(":+-ted-+:", _taintedValue.Split("in", StringSplitOptions.None)[1], () => _taintedValue.Split("in", StringSplitOptions.None)[1]);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithStringCountOptions_ResultIsNotTainted()
    {
        AssertNoneTainted(_untaintedString.Split(_taintedSeparator, 1, StringSplitOptions.None));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithStringCountOptions_ResultIsNotTainted2()
    {
        AssertNoneTainted(_untaintedString.Split("i", 1, StringSplitOptions.None));
    }

    [Fact]

    public void GivenATaintedObject_WhenCallingSplitWithCharAndOptions_ResultIsTainted()
    {
        var str = String.Concat(_taintedString, "|", _untaintedString);
        var expected = new string[] { _taintedString, _untaintedString };

        AssertEqual(expected, str.Split('|'));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharAndOptions_ResultIsTainted2()
    {
        var expected = new string[] { "One", "Two", "Three" };
        var results = ComposedTaintedString.Split('|');

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharAndOptions_ResultIsTainted3()
    {
        var str = String.Concat(_taintedString, "|", _untaintedString, "|", _otherTaintedString, "|", _otherUntaintedString);
        var expected = new string[] { _taintedString, _untaintedString, _otherTaintedString, _otherUntaintedString };

        AssertEqual(expected, str.Split('|'));
    }


    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharAndOptions_ResultIsTainted4()
    {
        var expected = new string[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine" };
        var results = AddTaintedString("One|Two|Three|Four|Five|Six|Seven|Eight|Nine").Split('|');

        AssertEqual(expected, results);
        AssertAllTainted(results);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharAndOptions_ResultIsTainted5()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ta-+:", _taintedValue.Split('i')[0], () => _taintedValue.Split('i', StringSplitOptions.None)[0]);
        AssertTaintedFormatWithOriginalCallCheck(":+-nted-+:", _taintedValue.Split('i')[1], () => _taintedValue.Split('i', StringSplitOptions.None)[1]);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharAndOptions_ResultIsTainted6()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ta-+:", _taintedValue.Split('i', StringSplitOptions.None)[0], () => _taintedValue.Split('i', StringSplitOptions.None)[0]);
        AssertTaintedFormatWithOriginalCallCheck(":+-nted-+:", _taintedValue.Split('i', StringSplitOptions.None)[1], () => _taintedValue.Split('i', StringSplitOptions.None)[1]);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharAndOptions_ResultIsNotTainted()
    {
        AssertNoneTainted(_untaintedString.Split('i', StringSplitOptions.None));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharIndexAndOptions_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-ta-+:", _taintedValue.Split('i', 2, StringSplitOptions.None)[0], () => _taintedValue.Split('i', 2, StringSplitOptions.None)[0]);
        AssertTaintedFormatWithOriginalCallCheck(":+-nted-+:", _taintedValue.Split('i', 2, StringSplitOptions.None)[1], () => _taintedValue.Split('i', 2, StringSplitOptions.None)[1]);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingSplitWithCharCountAndOptions_ResultIsNotTainted()
    {
        AssertNoneTainted(_untaintedString.Split('i', 2, StringSplitOptions.None));
    }

#endif
}
