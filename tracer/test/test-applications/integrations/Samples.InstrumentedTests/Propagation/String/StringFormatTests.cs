using System;
using System.Text;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Propagation.String;
public class StringFormatTests : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";
    private string _taintedValue2 = "TAINTED2";
    private string _taintedFormat2Args = "format{0}{1}";
    private string _taintedFormat3Args = "format{0}{1}{2}";
    private string _taintedFormat1Arg = "format{0}";
    private string _untaintedString = "UntaintedString";
    private string _otherUntaintedString = "OtherUntaintedString";

    public StringFormatTests()
    {
        AddTainted(_taintedValue);
        AddTainted(_taintedFormat1Arg);
        AddTainted(_taintedFormat2Args);
        AddTainted(_taintedFormat3Args);
        AddTainted(_taintedValue2);
    }

    // Testing public static string Format(IFormatProvider provider, string format, params object[] args)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: taintedcustomformat-+:", 
            System.String.Format(new FormatProviderForTest(), "test: {0}", new object[] { _taintedValue }), 
            () => System.String.Format(new FormatProviderForTest(), "test: {0}", new object[] { _taintedValue }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatUntaintedStringcustomformatTAINTED2customformat-+:", 
            System.String.Format(new FormatProviderForTest(), _taintedFormat2Args, new object[] { _untaintedString, _taintedValue2 }), 
            () => System.String.Format(new FormatProviderForTest(), _taintedFormat2Args, new object[] { _untaintedString, _taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted8()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatUntaintedStringcustomformatUntaintedStringcustomformat-+:", 
            System.String.Format(new FormatProviderForTest(), _taintedFormat2Args, new object[] { _untaintedString, _untaintedString }), 
            () => System.String.Format(new FormatProviderForTest(), _taintedFormat2Args, new object[] { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted3()
    {
        string str = "Literal with tainteds {0}{1} and untainted {2} and tainted {3} and another untainted {4}";
        AssertTaintedFormatWithOriginalCallCheck(":+-Literal with tainteds taintedcustomformatTAINTED2customformat and untainted UntaintedStringcustomformat and tainted TAINTED2customformat and another untainted OtherUntaintedStringcustomformat-+:",
            System.String.Format(new FormatProviderForTest(), str, _taintedValue, _taintedValue2, _untaintedString, _taintedValue2, _otherUntaintedString),
            () => System.String.Format(new FormatProviderForTest(), str, _taintedValue, _taintedValue2, _untaintedString, _taintedValue2, _otherUntaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted13()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwcustomformat-+:",
            System.String.Format(new FormatProviderForTest(), _taintedFormat1Arg, new object[] { "ww" }),
            () => System.String.Format(new FormatProviderForTest(), _taintedFormat1Arg, new object[] { "ww" }));
    }

    // Testing public static string Format(IFormatProvider provider, string format, object arg0, object arg1)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintedcustomformatTAINTED2customformat-+:",
            System.String.Format(new FormatProviderForTest(), _taintedFormat2Args, _taintedValue, _taintedValue2),
            () => System.String.Format(new FormatProviderForTest(), _taintedFormat2Args, _taintedValue, _taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted5()
    {
        Customer customer = new Customer(_taintedValue, 999654);
        AssertTaintedFormatWithOriginalCallCheck(":+-Tainted literal equals to tainted and number 0000-999-654-+:",
            System.String.Format(new CustomerNumberFormatter(), "Tainted literal equals to {0} and number {1}", customer.Name, customer.CustomerNumber),
            () => System.String.Format(new CustomerNumberFormatter(), "Tainted literal equals to {0} and number {1}", customer.Name, customer.CustomerNumber));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted14()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwcustomformatwwcustomformat-+:",
            System.String.Format(new FormatProviderForTest(), _taintedFormat2Args, "ww", "ww"),
            () => System.String.Format(new FormatProviderForTest(), _taintedFormat2Args, "ww", "ww"));
    }

    // Testing public static string Format(IFormatProvider provider, string format, object arg0)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted6()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintedcustomformat-+:",
            System.String.Format(new FormatProviderForTest(), "format{0}", _taintedValue),
            () => System.String.Format(new FormatProviderForTest(), "format{0}", _taintedValue));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted15()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwcustomformat-+:",
            System.String.Format(new FormatProviderForTest(), _taintedFormat1Arg, "ww"),
            () => System.String.Format(new FormatProviderForTest(), _taintedFormat1Arg, "ww"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted7()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", 
            System.String.Format(null, _taintedValue, _taintedValue2), 
            () => System.String.Format(null, _taintedValue, _taintedValue2));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithNullParams_FormatException()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => System.String.Format(null, null, "r"),
            () => System.String.Format(null, null, "r"));
    }

    // Testing public static string Format(IFormatProvider provider, string format, object arg0, object arg1, object arg2)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted9()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintedcustomformat TAINTED2customformat TAINTED2customformat-+:",
            System.String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _taintedValue, _taintedValue2, _taintedValue2),
            () => System.String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _taintedValue, _taintedValue2, _taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted10()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatUntaintedStringcustomformat taintedcustomformat UntaintedStringcustomformat-+:",
            System.String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _untaintedString, _taintedValue, _untaintedString),
            () => System.String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _untaintedString, _taintedValue, _untaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted11()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintedcustomformat UntaintedStringcustomformat UntaintedStringcustomformat-+:",
            System.String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _taintedValue, _untaintedString, _untaintedString),
            () => System.String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _taintedValue, _untaintedString, _untaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted12()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatUntaintedStringcustomformat UntaintedStringcustomformat taintedcustomformat-+:",
            System.String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _untaintedString, _untaintedString, _taintedValue),
            () => System.String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _untaintedString, _untaintedString, _taintedValue));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted16()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwcustomformatwwcustomformatwwcustomformat-+:",
            System.String.Format(new FormatProviderForTest(), _taintedFormat3Args, "ww", "ww", "ww"),
            () => System.String.Format(new FormatProviderForTest(), _taintedFormat3Args, "ww", "ww", "ww"));
    }

    // Testing public static string Format(string format, params object[] args)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithObjectArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted-+:", 
            System.String.Format("test: {0}", new object[] { _taintedValue }), 
            () => System.String.Format("test: {0}", new object[] { _taintedValue }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithObjectArray_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: UntaintedString tainted-+:", 
            System.String.Format("test: {0} {1}", new object[] { _untaintedString, _taintedValue }), 
            () => System.String.Format("test: {0} {1}", new object[] { _untaintedString, _taintedValue }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithObjectArray_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatww-+:",
            System.String.Format(_taintedFormat1Arg, new object[] { "ww" }),
            () => System.String.Format(_taintedFormat1Arg, new object[] { "ww" }));
    }

    // Testing public static string Format(string format, object arg0)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithOneObject_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted-+:", 
            System.String.Format("test: {0}", _taintedValue), 
            () => System.String.Format("test: {0}", _taintedValue));
    }

    [Fact]
    public void GivenANotTaintedObject_WhenCallingFormatWithOneObject_ResultIsNotTainted()
    {
        AssertNotTainted(System.String.Format("test: {0}", _untaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithNewStringBuilder_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted-+:", 
            System.String.Format("test: {0}", new System.Text.StringBuilder(_taintedValue)), 
            () => System.String.Format("test: {0}", new System.Text.StringBuilder(_taintedValue)));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithOneObjectLess_FormatException()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => System.String.Format(_taintedFormat2Args, _taintedValue),
            () => System.String.Format(_taintedFormat2Args, _taintedValue));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithOneObject_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatww-+:",
            System.String.Format(_taintedFormat1Arg, "ww"),
            () => System.String.Format(_taintedFormat1Arg, "ww"));
    }

    // Testing public static string Format(string format, object arg0, object arg1)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithTwoObjects_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted TAINTED2-+:", 
            System.String.Format("test: {0} {1}", _taintedValue, _taintedValue2), 
            () => System.String.Format("test: {0} {1}", _taintedValue, _taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithTwoObjects_ResultIsTainted2()
    {
        string str = "{0} and Literal equals to {1}";
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted and Literal equals to UntaintedString-+:",
            System.String.Format(str, _taintedValue, _untaintedString),
            () => System.String.Format(str, _taintedValue, _untaintedString));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithTwoObjects_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatUntaintedStringTAINTED2-+:", 
            System.String.Format(_taintedFormat2Args, _untaintedString, _taintedValue2), 
            () => System.String.Format(_taintedFormat2Args, _untaintedString, _taintedValue2));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithTwoObjects_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwww-+:",
            System.String.Format(_taintedFormat2Args, "ww", "ww"),
            () => System.String.Format(_taintedFormat2Args, "ww", "ww"));
    }

    // Testing public static string Format(string format, object arg0, object arg1, object arg2)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithThreebjects_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: www www TAINTED2-+:", 
            System.String.Format("test: {0} {1} {2}", "www", "www", _taintedValue2), 
            () => System.String.Format("test: {0} {1} {2}", "www", "www", _taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithThreebjects_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: www tainted www-+:", 
            System.String.Format("test: {0} {1} {2}", "www", _taintedValue, "www"), 
            () => System.String.Format("test: {0} {1} {2}", "www", _taintedValue, "www"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithThreebjects_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted www www-+:", 
            System.String.Format("test: {0} {1} {2}", _taintedValue, "www", "www"), 
            () => System.String.Format("test: {0} {1} {2}", _taintedValue, "www", "www"));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormat_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattainted TAINTED2 TAINTED2-+:",
            System.String.Format("format{0} {1} {2}", _taintedValue, _taintedValue2, _taintedValue2),
            () => System.String.Format("format{0} {1} {2}", _taintedValue, _taintedValue2, _taintedValue2));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormat_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwwwww-+:",
            System.String.Format(_taintedFormat3Args, "ww", "ww", "ww"),
            () => System.String.Format(_taintedFormat3Args, "ww", "ww", "ww"));
    }

    // Testing public static string Format(string format, params object[] args)

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintedTAINTED2-+:", 
            System.String.Format(_taintedFormat2Args, new object[] { _taintedValue, _taintedValue2 }), 
            () => System.String.Format(_taintedFormat2Args, new object[] { _taintedValue, _taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectArray_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintednotTainted-+:", 
            System.String.Format(_taintedFormat2Args, new object[] { _taintedValue, "notTainted" }), 
            () => System.String.Format(_taintedFormat2Args, new object[] { _taintedValue, "notTainted" }));
    }

    [Fact]
    public void GivenANotTaintedFormatObject_WhenCallingFormatWithObjectArray_ResultIsNotTainted2()
    {
        AssertUntaintedWithOriginalCallCheck("FormatnotTaintednotTainted",
            System.String.Format("Format{0}{1}", new object[] { "notTainted", "notTainted" }),
            () => System.String.Format("Format{0}{1}", new object[] { "notTainted", "notTainted" }));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectArray_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-abcnotTaintedtainted-+:", 
            System.String.Format("abc{0}{1}", new object[] { "notTainted", _taintedValue }), 
            () => System.String.Format("abc{0}{1}", new object[] { "notTainted", _taintedValue }));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectArray_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwwwww-+:",
            System.String.Format(_taintedFormat3Args, new object[] { "ww", "ww", "ww" }),
            () => System.String.Format(_taintedFormat3Args, new object[] { "ww", "ww", "ww" }));
    }

    [Fact]
    public void GivenANotTaintedFormatObject_WhenCallingFormatWithTaintedObjectNoReplace_ResultIsNotTainted2()
    {
        AssertUntaintedWithOriginalCallCheck("Format",
            System.String.Format("Format", _taintedValue),
            () => System.String.Format("Format", _taintedValue));
    }


#if NET8_0_OR_GREATER
    // System.String Format(System.IFormatProvider, System.Text.CompositeFormat, System.Object[])

    [Fact]
    public void GivenATaintedString_WhenCallingStringFormatObjectArrayFormatProvider_ResultIsTainted2()
    {
        var composite = CompositeFormat.Parse("myformat{0}{1}");
        AssertUntaintedWithOriginalCallCheck(
            "myformattaintedcustomformatUntaintedStringcustomformat",
            System.String.Format(new FormatProviderForTest(), composite, new object[] { _taintedValue, _untaintedString }).ToString(),
            () => System.String.Format(new FormatProviderForTest(), composite, new object[] { _taintedValue, _untaintedString }).ToString());
    }
#endif

#if NET9_0_OR_GREATER

    // Testing public static string Format(string format, ReadOnlySpan<object>)

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectReadOnlySpan_ResultIsTainted()
    {
        var values = new object[] { _taintedValue, _taintedValue2 };
        var span = new ReadOnlySpan<object>(values);
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintedTAINTED2-+:",
            System.String.Format(_taintedFormat2Args, span),
            () => System.String.Format(_taintedFormat2Args, values));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectReadOnlySpan_ResultIsTainted2()
    {
        var values = new object[] { _taintedValue, "notTainted" };
        var span = new ReadOnlySpan<object>(values);
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintednotTainted-+:",
            System.String.Format(_taintedFormat2Args, span),
            () => System.String.Format(_taintedFormat2Args, values));
    }

    [Fact]
    public void GivenANotTaintedFormatObject_WhenCallingFormatWithObjectReadOnlySpan_ResultIsNotTainted2()
    {
        var values = new object[] { "notTainted", "notTainted" };
        var span = new ReadOnlySpan<object>(values);
        AssertUntaintedWithOriginalCallCheck("FormatnotTaintednotTainted",
            System.String.Format("Format{0}{1}", span),
            () => System.String.Format("Format{0}{1}", values));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectReadOnlySpan_ResultIsTainted3()
    {
        var values = new object[] { "notTainted", _taintedValue };
        var span = new ReadOnlySpan<object>(values);
        AssertTaintedFormatWithOriginalCallCheck(":+-abcnotTaintedtainted-+:",
            System.String.Format("abc{0}{1}", span),
            () => System.String.Format("abc{0}{1}", values));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectReadOnlySpan_ResultIsTainted4()
    {
        var values = new object[] { "ww", "ww", "ww" };
        var span = new ReadOnlySpan<object>(values);
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwwwww-+:",
            System.String.Format(_taintedFormat3Args, span),
            () => System.String.Format(_taintedFormat3Args, values));
    }

    // Testing public static string Format(IFormatProvider provider, string format, ReadOnlySpan<object>)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProviderAndObjectReadOnlySpan_ResultIsTainted()
    {
        var values = new object[] { _taintedValue };
        var span = new ReadOnlySpan<object>(values);
        AssertTaintedFormatWithOriginalCallCheck(":+-test: taintedcustomformat-+:",
            System.String.Format(new FormatProviderForTest(), "test: {0}", span),
            () => System.String.Format(new FormatProviderForTest(), "test: {0}", values));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProviderAndObjectReadOnlySpan_ResultIsTainted2()
    {
        var values = new object[] { _untaintedString, _taintedValue2 };
        var span = new ReadOnlySpan<object>(values);
        AssertTaintedFormatWithOriginalCallCheck(":+-formatUntaintedStringcustomformatTAINTED2customformat-+:",
            System.String.Format(new FormatProviderForTest(), _taintedFormat2Args, span),
            () => System.String.Format(new FormatProviderForTest(), _taintedFormat2Args, values));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProviderAndObjectReadOnlySpan_ResultIsTainted8()
    {
        var values = new object[] { _untaintedString, _untaintedString };
        var span = new ReadOnlySpan<object>(values);
        AssertTaintedFormatWithOriginalCallCheck(":+-formatUntaintedStringcustomformatUntaintedStringcustomformat-+:",
            System.String.Format(new FormatProviderForTest(), _taintedFormat2Args, span),
            () => System.String.Format(new FormatProviderForTest(), _taintedFormat2Args, values));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProviderAndObjectReadOnlySpan_ResultIsTainted3()
    {
        var values = new object[] { _taintedValue, _taintedValue2, _untaintedString, _taintedValue2, _otherUntaintedString };
        var span = new ReadOnlySpan<object>(values);
        string str = "Literal with tainteds {0}{1} and untainted {2} and tainted {3} and another untainted {4}";
        AssertTaintedFormatWithOriginalCallCheck(":+-Literal with tainteds taintedcustomformatTAINTED2customformat and untainted UntaintedStringcustomformat and tainted TAINTED2customformat and another untainted OtherUntaintedStringcustomformat-+:",
            System.String.Format(new FormatProviderForTest(), str, span),
            () => System.String.Format(new FormatProviderForTest(), str, values));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProviderAndObjectReadOnlySpan_ResultIsTainted13()
    {
        var values = new object[] { "ww" };
        var span = new ReadOnlySpan<object>(values);
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwcustomformat-+:",
            System.String.Format(new FormatProviderForTest(), _taintedFormat1Arg, span),
            () => System.String.Format(new FormatProviderForTest(), _taintedFormat1Arg, values));
    }

#endif

}
