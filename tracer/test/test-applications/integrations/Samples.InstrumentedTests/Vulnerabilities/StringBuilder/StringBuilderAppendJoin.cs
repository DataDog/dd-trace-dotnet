#if NETCOREAPP3_1_OR_GREATER
using System.Collections.Generic;
using System.Text;
using Xunit;
namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringBuilderPropagation;

public class StringBuilderAppendJoin : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";
    private string _taintedValue2 = "tainted2";
    private string _untaintedString = "untainted";

    public StringBuilderAppendJoin()
    {
        AddTainted(_taintedValue);
        AddTainted(_taintedValue2);
    }

    // System.Text.StringBuilder::AppendJoin(System.String,System.String[])

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringString_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted.untainted-+:", 
            new StringBuilder().AppendJoin(".", new string[] { _taintedValue, _untaintedString }),
            () => new StringBuilder().AppendJoin(".", new string[] { _taintedValue, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringString_ThenResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-untainted.tainted-+:",
            new StringBuilder().AppendJoin(".", new string[] { _untaintedString, _taintedValue }),
            () => new StringBuilder().AppendJoin(".", new string[] { _untaintedString, _taintedValue }));
    }

    [Fact]
    public void GivenAStringBuilderNotTainted_WhenAppendJoinStringString_ThenResultIsNotTainted()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder().AppendJoin(".", new string[] { _untaintedString, _untaintedString }),
            () => new StringBuilder().AppendJoin(".", new string[] { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringString_ThenResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-testuntainted.untainted-+:",
            GetTaintedStringBuilder("test").AppendJoin(".", new string[] { _untaintedString, _untaintedString }),
            () => GetTaintedStringBuilder("test").AppendJoin(".", new string[] { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringString_ThenResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-testuntainteduntainted-+:",
            GetTaintedStringBuilder("test").AppendJoin((string) null, new string[] { _untaintedString, _untaintedString }),
            () => GetTaintedStringBuilder("test").AppendJoin((string) null, new string[] { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringString_ThenResultIsNotTainted5()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => GetTaintedStringBuilder("test").AppendJoin((string)null, (string[]) null),
            () => GetTaintedStringBuilder("test").AppendJoin((string)null, (string[]) null));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringString_ThenResultIsNotTainted6()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => GetTaintedStringBuilder("test").AppendJoin(".", (string[]) null),
            () => GetTaintedStringBuilder("test").AppendJoin(".", (string[]) null));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringString_ThenResultIsTainted7()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-untaintedtainteduntainted-+:",
            new StringBuilder().AppendJoin(_taintedValue, new string[] { _untaintedString, _untaintedString }),
            () => new StringBuilder().AppendJoin(_taintedValue, new string[] { _untaintedString, _untaintedString }));
    }

    // System.Text.StringBuilder::AppendJoin(System.String,System.Object[])

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringObject_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted.untainted-+:",
            new StringBuilder().AppendJoin(".", new object[] { _taintedValue, _untaintedString }),
            () => new StringBuilder().AppendJoin(".", new object[] { _taintedValue, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringObject_ThenResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-untainted.tainted-+:",
            new StringBuilder().AppendJoin(".", new object[] { _untaintedString, _taintedValue }),
            () => new StringBuilder().AppendJoin(".", new object[] { _untaintedString, _taintedValue }));
    }

    [Fact]
    public void GivenAStringBuilderNotTainted_WhenAppendJoinStringObject_ThenResultIsNotTainted()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder().AppendJoin(".", new object[] { _untaintedString, _untaintedString }),
            () => new StringBuilder().AppendJoin(".", new object[] { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringObject_ThenResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-testuntainted.untainted-+:",
            GetTaintedStringBuilder("test").AppendJoin(".", new object[] { _untaintedString, _untaintedString }),
            () => GetTaintedStringBuilder("test").AppendJoin(".", new object[] { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringObject_ThenResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-testuntainteduntainted-+:",
            GetTaintedStringBuilder("test").AppendJoin((string)null, new object[] { _untaintedString, _untaintedString }),
            () => GetTaintedStringBuilder("test").AppendJoin((string)null, new object[] { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringObject_ThenResultIsNotTainted5()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => GetTaintedStringBuilder("test").AppendJoin((string)null, (object[]) null),
            () => GetTaintedStringBuilder("test").AppendJoin((string)null, (object[]) null));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringObject_ThenResultIsNotTainted6()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => GetTaintedStringBuilder("test").AppendJoin(".", (object[]) null),
            () => GetTaintedStringBuilder("test").AppendJoin(".", (object[]) null));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringObject_ThenResultIsTainted7()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-untaintedtainteduntainted-+:",
            new StringBuilder().AppendJoin(_taintedValue, new object[] { _untaintedString, _untaintedString }),
            () => new StringBuilder().AppendJoin(_taintedValue, new object[] { _untaintedString, _untaintedString }));
    }

    // System.Text.StringBuilder::AppendJoin(System.String,System.Collections.Generic.IEnumerable`1<!!0>)

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringIEnumerable_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted.untainted-+:",
            new StringBuilder().AppendJoin(".", new List<object> { _taintedValue, _untaintedString }),
            () => new StringBuilder().AppendJoin(".", new List<object> { _taintedValue, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringIEnumerable_ThenResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-untainted.tainted-+:",
            new StringBuilder().AppendJoin(".", new List<object> { _untaintedString, _taintedValue }),
            () => new StringBuilder().AppendJoin(".", new List<object> { _untaintedString, _taintedValue }));
    }

    [Fact]
    public void GivenAStringBuilderNotTainted_WhenAppendJoinStringIEnumerable_ThenResultIsNotTainted()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder().AppendJoin(".", new List<object> { _untaintedString, _untaintedString }),
            () => new StringBuilder().AppendJoin(".", new List<object> { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringIEnumerable_ThenResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-testuntainted.untainted-+:",
            GetTaintedStringBuilder("test").AppendJoin(".", new List<object> { _untaintedString, _untaintedString }),
            () => GetTaintedStringBuilder("test").AppendJoin(".", new List<object> { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringIEnumerable_ThenResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-testuntainteduntainted-+:",
            GetTaintedStringBuilder("test").AppendJoin((string)null, new List<object> { _untaintedString, _untaintedString }),
            () => GetTaintedStringBuilder("test").AppendJoin((string)null, new List<object> { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringIEnumerable_ThenResultIsNotTainted5()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => GetTaintedStringBuilder("test").AppendJoin((string)null, (List<object>)null),
            () => GetTaintedStringBuilder("test").AppendJoin((string)null, (List<object>)null));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringIEnumerable_ThenResultIsNotTainted6()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => GetTaintedStringBuilder("test").AppendJoin(".", (object[])null),
            () => GetTaintedStringBuilder("test").AppendJoin(".", (object[])null));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinStringIEnumerable_ThenResultIsTainted7()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-untaintedtainteduntainted-+:",
            new StringBuilder().AppendJoin(_taintedValue, new List<object> { _untaintedString, _untaintedString }),
            () => new StringBuilder().AppendJoin(_taintedValue, new List<object> { _untaintedString, _untaintedString }));
    }

    // System.Text.StringBuilder::AppendJoin(System.Char,System.Char[])

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharString_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted.untainted-+:",
            new StringBuilder().AppendJoin('.', new string[] { _taintedValue, _untaintedString }),
            () => new StringBuilder().AppendJoin('.', new string[] { _taintedValue, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharString_ThenResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-untainted.tainted-+:",
            new StringBuilder().AppendJoin('.', new string[] { _untaintedString, _taintedValue }),
            () => new StringBuilder().AppendJoin('.', new string[] { _untaintedString, _taintedValue }));
    }

    [Fact]
    public void GivenAStringBuilderNotTainted_WhenAppendJoinCharString_ThenResultIsNotTainted()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder().AppendJoin('.', new string[] { _untaintedString, _untaintedString }),
            () => new StringBuilder().AppendJoin('.', new string[] { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharString_ThenResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-testuntainted.untainted-+:",
            GetTaintedStringBuilder("test").AppendJoin('.', new string[] { _untaintedString, _untaintedString }),
            () => GetTaintedStringBuilder("test").AppendJoin('.', new string[] { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharString_ThenResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-testuntainteduntainted-+:",
            GetTaintedStringBuilder("test").AppendJoin((string)null, new string[] { _untaintedString, _untaintedString }),
            () => GetTaintedStringBuilder("test").AppendJoin((string)null, new string[] { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharString_ThenResultIsNotTainted5()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => GetTaintedStringBuilder("test").AppendJoin((string)null, (string[])null),
            () => GetTaintedStringBuilder("test").AppendJoin((string)null, (string[])null));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharString_ThenResultIsNotTainted6()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => GetTaintedStringBuilder("test").AppendJoin('.', (string[])null),
            () => GetTaintedStringBuilder("test").AppendJoin('.', (string[])null));
    }

    // System.Text.StringBuilder::AppendJoin(System.Char,System.Object[])

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharObject_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted.untainted-+:",
            new StringBuilder().AppendJoin('.', new object[] { _taintedValue, _untaintedString }),
            () => new StringBuilder().AppendJoin('.', new object[] { _taintedValue, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharObject_ThenResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-untainted.tainted-+:",
            new StringBuilder().AppendJoin('.', new object[] { _untaintedString, _taintedValue }),
            () => new StringBuilder().AppendJoin('.', new object[] { _untaintedString, _taintedValue }));
    }

    [Fact]
    public void GivenAStringBuilderNotTainted_WhenAppendJoinCharObject_ThenResultIsNotTainted()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder().AppendJoin('.', new object[] { _untaintedString, _untaintedString }),
            () => new StringBuilder().AppendJoin('.', new object[] { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharObject_ThenResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-testuntainted.untainted-+:",
            GetTaintedStringBuilder("test").AppendJoin('.', new object[] { _untaintedString, _untaintedString }),
            () => GetTaintedStringBuilder("test").AppendJoin('.', new object[] { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharObject_ThenResultIsNotTainted6()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => GetTaintedStringBuilder("test").AppendJoin('.', (object[])null),
            () => GetTaintedStringBuilder("test").AppendJoin('.', (object[])null));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharIEnumerable_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted.untainted-+:",
            new StringBuilder().AppendJoin('.', new List<object> { _taintedValue, _untaintedString }),
            () => new StringBuilder().AppendJoin('.', new List<object> { _taintedValue, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharIEnumerable_ThenResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-untainted.tainted-+:",
            new StringBuilder().AppendJoin('.', new List<object> { _untaintedString, _taintedValue }),
            () => new StringBuilder().AppendJoin('.', new List<object> { _untaintedString, _taintedValue }));
    }

    [Fact]
    public void GivenAStringBuilderNotTainted_WhenAppendJoinCharIEnumerable_ThenResultIsNotTainted()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder().AppendJoin('.', new List<object> { _untaintedString, _untaintedString }),
            () => new StringBuilder().AppendJoin('.', new List<object> { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharIEnumerable_ThenResultIsTainted3()
    {
        var st = new StringBuilder("test").AppendJoin('.', new List<object> { _untaintedString, _untaintedString });

        AssertTaintedFormatWithOriginalCallCheck(":+-testuntainted.untainted-+:",
            GetTaintedStringBuilder("test").AppendJoin('.', new List<object> { _untaintedString, _untaintedString }),
            () => GetTaintedStringBuilder("test").AppendJoin('.', new List<object> { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharIEnumerable_ThenResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test3.4-+:",
            GetTaintedStringBuilder("test").AppendJoin('.', new List<int> { 3, 4 }),
            () => GetTaintedStringBuilder("test").AppendJoin('.', new List<int> { 3, 4 }));
    }

    [Fact]
    public void GivenAStringBuilderTainted_WhenAppendJoinCharIEnumerable_ThenResultIsNotTainted()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => GetTaintedStringBuilder("test").AppendJoin('.', (object[])null),
            () => GetTaintedStringBuilder("test").AppendJoin('.', (object[])null));
    }
}
#endif
