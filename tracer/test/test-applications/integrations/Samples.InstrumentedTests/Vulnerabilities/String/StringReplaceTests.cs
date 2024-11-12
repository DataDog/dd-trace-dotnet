using System;
using System.Globalization;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;
public class StringReplaceTests : InstrumentationTestsBase
{
    private string _taintedString = "TaintedString";
    private string _untaintedString = "UntaintedString";
    private string _otherTaintedString = "OtherTaintedString";
    private string _largeTaintedString = "LargeTaintedString";
    private string _taintedValue = "tainted";
    private string _taintedSplit = "int";
    private string _largeString = "LargeString";

    public StringReplaceTests()
    {
        AddTainted(_taintedString);
        AddTainted(_otherTaintedString);
        AddTainted(_taintedValue);
        AddTainted(_taintedSplit);
        AddTainted(_largeTaintedString);
    }

    // Testing Replace(string oldValue, string? newValue)

    [Fact]
    public void GivenAString_WhenCallingReplace_ResultIsOk()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => "weww".Replace("e", "r"),
            () => "weww".Replace("e", "r"));
    }

    [Fact]
    public void GivenAString_WhenCallingReplace_ResultIsOk2()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => "weww".Replace(null, "r"),
            () => "weww".Replace(null, "r"));
    }

    [Fact]
    public void GivenAString_WhenCallingReplace_ResultIsOk3()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => "weww".Replace("e", null),
            () => "weww".Replace("e", null));
    }

    [Fact]
    public void GivenAString_WhenCallingReplace_ResultIsOk4()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => ((string)null).Replace("e", "r"),
            () => ((string)null).Replace("e", "r"));
    }

    [Fact]
    public void GivenAString_WhenCallingReplace_ResultIsOk6()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-taintd-+:",
            _taintedValue.Replace("e", null),
            () => _taintedValue.Replace("e", null));
    }

    [Fact]
    public void GivenAString_WhenCallingReplace_ResultIsOk7()
    {
        string test = null;
        AssertUntaintedWithOriginalCallCheck(
            () => test.Replace("e", null),
            () => test.Replace("e", null));
    }

    [Fact]
    public void GivenAString_WhenCallingReplace_ResultIsOk8()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => _taintedValue.Replace(null, null),
            () => _taintedValue.Replace(null, null));
    }

    [Fact]
    public void GivenAString_WhenCallingReplace_ResultIsOk9()
    {
        string test = null;
        AssertUntaintedWithOriginalCallCheck(
            () => test.Replace(null, null),
            () => test.Replace(null, null));
    }

    [Fact]
    public void GivenAString_WhenCallingReplace_ResultIsOk10()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => _taintedValue.Replace(String.Empty, "w"),
            () => _taintedValue.Replace(String.Empty, "w"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWithStringParameters_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tbinted-+:", _taintedValue.Replace("a", "b"), () => _taintedValue.Replace("a", "b"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWithStringParametersInFirstPosition_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tinted-+:", _taintedValue.Replace("ta", "t"), () => _taintedValue.Replace("ta", "t"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWithStringParameters_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tabed-+:", _taintedValue.Replace(_taintedSplit, "b"), () => _taintedValue.Replace(_taintedSplit, "b"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWithStringParameters_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-qinterty-+:", ("qwerty").Replace("w", _taintedSplit), () => ("qwerty").Replace("w", _taintedSplit));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWithNullStringParameters_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tinted-+:",
            _taintedValue.Replace("a", null),
            () => _taintedValue.Replace("a", null));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWithEmptyStringParameters_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tinted-+:", _taintedValue.Replace("a", ""), () => _taintedValue.Replace("a", ""));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted21()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-LargeUntaintedString-+:",
            _largeTaintedString.Replace(_taintedString, _untaintedString),
            () => _largeTaintedString.Replace(_taintedString, _untaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted20()
    {
        string str = String.Concat(_taintedString, " joining ", _otherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedLarge joining OtherTaintedLarge-+:",
            str.Replace("String", "Large"),
            () => str.Replace("String", "Large"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted19()
    {
        string str = String.Concat(_taintedString, " joining ", _otherTaintedString, " and ", _largeTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedSmall joining OtherTaintedSmall and LargeTaintedSmall-+:",
            str.Replace("String", "Small"),
            () => str.Replace("String", "Small"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted18()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-LargeOtherTaintedString-+:",
            _largeTaintedString.Replace(_taintedString, _otherTaintedString),
            () => _largeTaintedString.Replace(_taintedString, _otherTaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainte17()
    {
        string str = String.Concat(_otherTaintedString, " joining ", _otherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-OtherLargeString joining OtherLargeString-+:",
            str.Replace(_taintedString, _largeString),
            () => str.Replace(_taintedString, _largeString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted16()
    {
        string str = String.Concat(_taintedString, " joining ", _otherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-LargeString joining OtherLargeString-+:",
            str.Replace(_taintedString, _largeString),
            () => str.Replace(_taintedString, _largeString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted15()
    {
        string str = String.Concat(_otherTaintedString, " joining ", _otherTaintedString, " and ", _largeTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-OtherLargeString joining OtherLargeString and LargeLargeString-+:",
            str.Replace(_taintedString, _largeString),
            () => str.Replace(_taintedString, _largeString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted14()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-LargeXaintedString-+:",
            _largeTaintedString.Replace('T', 'X'),
            () => _largeTaintedString.Replace("T", "X"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted13()
    {
        string str = String.Concat(_taintedString, " joining ", _otherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-XaintedString joining OtherXaintedString-+:",
            str.Replace("T", "X"),
            () => str.Replace("T", "X"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted12()
    {
        string str = String.Concat(_taintedString, " joining ", _otherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedXtring joining OtherTaintedXtring-+:",
            str.Replace("S", "X"),
            () => str.Replace("S", "X"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted11()
    {
        string str = String.Concat(_taintedString, " joining ", _otherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedStrinX joininX OtherTaintedStrinX-+:",
            str.Replace("g", "X"),
            () => str.Replace("g", "X"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted410()
    {
        string str = String.Concat(_taintedString, " joining ", _otherTaintedString, " and ", _largeTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedXtring joining OtherTaintedXtring and LargeTaintedXtring-+:",
            str.Replace("S", "X"),
            () => str.Replace("S", "X"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted9()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-LargeTaintedString-+:",
            _largeTaintedString.Replace("Large*", _untaintedString),
            () => _largeTaintedString.Replace("Large*", _untaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted8()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-LargeTaintedString-+:",
            _largeTaintedString.Replace("^La$", _untaintedString),
            () => _largeTaintedString.Replace("^La$", _untaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted7()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-LaaaaargeTaintedString-+:",
            AddTainted("LaaaaargeTaintedString").ToString().Replace(@"\d+", _untaintedString),
            () => AddTainted("LaaaaargeTaintedString").ToString().Replace(@"\d+", _untaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted6()
    {
        string str = String.Concat("-", "Payload", "-", _otherTaintedString, "end");
        // if no replacement is done, string remains inmutable
        AssertTaintedFormatWithOriginalCallCheck("-Payload-:+-OtherTaintedString-+:end",
            str.Replace(@"-Payload-([A-Za-z0-9\-]+)\end", _untaintedString),
            () => str.Replace(@"-Payload-([A-Za-z0-9\-]+)\end", _untaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted5()
    {
        string str = String.Concat(_taintedString, " joining ", _otherTaintedString, " and ", _largeTaintedString);
        // if no replacement is done, string remains inmutable
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedString-+: joining :+-OtherTaintedString-+: and :+-LargeTaintedString-+:",
            str.Replace("^[a-zA-Z]*$", "Small"),
            () => str.Replace("^[a-zA-Z]*$", "Small"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted4()
    {
        string str = String.Concat(_taintedString, " joining ", _otherTaintedString, " and ", _largeTaintedString);
        // if no replacement is done, string remains inmutable
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedString-+: joining :+-OtherTaintedString-+: and :+-LargeTaintedString-+:",
            str.Replace(@"T.*g-+:", "Small"),
            () => str.Replace(@"T.*g-+:", "Small"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWith2Parameters_ResultIsTainted()
    {
        string str = String.Concat("dummy", _taintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-duOtherTaintedStringTaintedString-+:",
            str.Replace("mmy", _otherTaintedString),
            () => str.Replace("mmy", _otherTaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWithNullFirstStringParameters_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentNullException>(() => _taintedValue.Replace(null, "e"));
    }

    // testing string Replace(char oldChar, char newChar)

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWithCharParameters_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tbinted-+:", _taintedValue.Replace('a', 'b'), () => _taintedValue.Replace('a', 'b'));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWithCharParameters_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-bainbed-+:", _taintedValue.Replace('t', 'b'), () => _taintedValue.Replace('t', 'b'));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplaceWithCharParametersNoReplace_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", _taintedValue.Replace('%', 'e'), () => _taintedValue.Replace('%', 'e'));
    }

    [Fact]
    public void GivenANonTaintedString_WhenCallingReplaceWithUntaintedReplace_ResultIsNonTainted()
    {
        AssertUntaintedWithOriginalCallCheck("UNT*new*INTED", "UNTAINTED".Replace("A", "*new*"), () => "UNTAINTED".Replace("A", "*new*"));
    }

    [Fact]
    public void GivenANonTaintedString_WhenCallingReplaceWithTaintedNoReplace_ResultIsNonTainted()
    {
        AssertUntaintedWithOriginalCallCheck("UNTAINTED", "UNTAINTED".Replace("%", _taintedValue), () => "UNTAINTED".Replace("%", _taintedValue));
    }

    [Fact]
    public void GivenANonTaintedString_WhenCallingReplaceWithTaintedReplace_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-UNtaintedAINtaintedED-+:", "UNTAINTED".Replace("T", _taintedValue), () => "UNTAINTED".Replace("T", _taintedValue));
    }

    // testing Replace(string oldValue, string? newValue, bool ignoreCase, CultureInfo? culture)

#if NETCOREAPP3_1_OR_GREATER
    [Fact]
    public void GivenATaintedObject_WhenCallingReplace_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tinted-+:",
            _taintedValue.Replace("a", "", true, CultureInfo.InvariantCulture),
            () => _taintedValue.Replace("a", "", true, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingReplace_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tinted-+:",
            _taintedValue.Replace("A", "", true, CultureInfo.InvariantCulture),
            () => _taintedValue.Replace("A", "", true, CultureInfo.InvariantCulture));
    }

    // testing Replace(string oldValue, string? newValue, StringComparison comparisonType)

    [Fact]
    public void GivenATaintedObject_WhenCallingReplace_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tinted-+:",
            _taintedValue.Replace("A", "", StringComparison.OrdinalIgnoreCase),
            () => _taintedValue.Replace("A", "", StringComparison.OrdinalIgnoreCase));
    }
#endif
}
