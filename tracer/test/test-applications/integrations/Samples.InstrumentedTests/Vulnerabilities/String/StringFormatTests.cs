using System;
using System.Text;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;
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
            String.Format(new FormatProviderForTest(), "test: {0}", new object[] { _taintedValue }), 
            () => String.Format(new FormatProviderForTest(), "test: {0}", new object[] { _taintedValue }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatUntaintedStringcustomformatTAINTED2customformat-+:", 
            String.Format(new FormatProviderForTest(), _taintedFormat2Args, new object[] { _untaintedString, _taintedValue2 }), 
            () => String.Format(new FormatProviderForTest(), _taintedFormat2Args, new object[] { _untaintedString, _taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted8()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatUntaintedStringcustomformatUntaintedStringcustomformat-+:", 
            String.Format(new FormatProviderForTest(), _taintedFormat2Args, new object[] { _untaintedString, _untaintedString }), 
            () => String.Format(new FormatProviderForTest(), _taintedFormat2Args, new object[] { _untaintedString, _untaintedString }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted3()
    {
        string str = "Literal with tainteds {0}{1} and untainted {2} and tainted {3} and another untainted {4}";
        AssertTaintedFormatWithOriginalCallCheck(":+-Literal with tainteds taintedcustomformatTAINTED2customformat and untainted UntaintedStringcustomformat and tainted TAINTED2customformat and another untainted OtherUntaintedStringcustomformat-+:",
            String.Format(new FormatProviderForTest(), str, _taintedValue, _taintedValue2, _untaintedString, _taintedValue2, _otherUntaintedString),
            () => String.Format(new FormatProviderForTest(), str, _taintedValue, _taintedValue2, _untaintedString, _taintedValue2, _otherUntaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted13()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwcustomformat-+:",
            String.Format(new FormatProviderForTest(), _taintedFormat1Arg, new object[] { "ww" }),
            () => String.Format(new FormatProviderForTest(), _taintedFormat1Arg, new object[] { "ww" }));
    }

    // Testing public static string Format(IFormatProvider provider, string format, object arg0, object arg1)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintedcustomformatTAINTED2customformat-+:",
            String.Format(new FormatProviderForTest(), _taintedFormat2Args, _taintedValue, _taintedValue2),
            () => String.Format(new FormatProviderForTest(), _taintedFormat2Args, _taintedValue, _taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted5()
    {
        Customer customer = new Customer(_taintedValue, 999654);
        AssertTaintedFormatWithOriginalCallCheck(":+-Tainted literal equals to tainted and number 0000-999-654-+:",
            String.Format(new CustomerNumberFormatter(), "Tainted literal equals to {0} and number {1}", customer.Name, customer.CustomerNumber),
            () => String.Format(new CustomerNumberFormatter(), "Tainted literal equals to {0} and number {1}", customer.Name, customer.CustomerNumber));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted14()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwcustomformatwwcustomformat-+:",
            String.Format(new FormatProviderForTest(), _taintedFormat2Args, "ww", "ww"),
            () => String.Format(new FormatProviderForTest(), _taintedFormat2Args, "ww", "ww"));
    }

    // Testing public static string Format(IFormatProvider provider, string format, object arg0)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted6()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintedcustomformat-+:",
            String.Format(new FormatProviderForTest(), "format{0}", _taintedValue),
            () => String.Format(new FormatProviderForTest(), "format{0}", _taintedValue));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted15()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwcustomformat-+:",
            String.Format(new FormatProviderForTest(), _taintedFormat1Arg, "ww"),
            () => String.Format(new FormatProviderForTest(), _taintedFormat1Arg, "ww"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted7()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", 
            String.Format(null, _taintedValue, _taintedValue2), 
            () => String.Format(null, _taintedValue, _taintedValue2));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithNullParams_FormatException()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => String.Format(null, null, "r"),
            () => String.Format(null, null, "r"));
    }

    // Testing public static string Format(IFormatProvider provider, string format, object arg0, object arg1, object arg2)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted9()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintedcustomformat TAINTED2customformat TAINTED2customformat-+:",
            String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _taintedValue, _taintedValue2, _taintedValue2),
            () => String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _taintedValue, _taintedValue2, _taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted10()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatUntaintedStringcustomformat taintedcustomformat UntaintedStringcustomformat-+:",
            String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _untaintedString, _taintedValue, _untaintedString),
            () => String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _untaintedString, _taintedValue, _untaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted11()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintedcustomformat UntaintedStringcustomformat UntaintedStringcustomformat-+:",
            String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _taintedValue, _untaintedString, _untaintedString),
            () => String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _taintedValue, _untaintedString, _untaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted12()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatUntaintedStringcustomformat UntaintedStringcustomformat taintedcustomformat-+:",
            String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _untaintedString, _untaintedString, _taintedValue),
            () => String.Format(new FormatProviderForTest(), "format{0} {1} {2}", _untaintedString, _untaintedString, _taintedValue));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted16()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwcustomformatwwcustomformatwwcustomformat-+:",
            String.Format(new FormatProviderForTest(), _taintedFormat3Args, "ww", "ww", "ww"),
            () => String.Format(new FormatProviderForTest(), _taintedFormat3Args, "ww", "ww", "ww"));
    }

    // Testing public static string Format(string format, params object[] args)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithObjectArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted-+:", 
            String.Format("test: {0}", new object[] { _taintedValue }), 
            () => String.Format("test: {0}", new object[] { _taintedValue }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithObjectArray_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: UntaintedString tainted-+:", 
            String.Format("test: {0} {1}", new object[] { _untaintedString, _taintedValue }), 
            () => String.Format("test: {0} {1}", new object[] { _untaintedString, _taintedValue }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithObjectArray_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatww-+:",
            String.Format(_taintedFormat1Arg, new object[] { "ww" }),
            () => String.Format(_taintedFormat1Arg, new object[] { "ww" }));
    }

    // Testing public static string Format(string format, object arg0)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithOneObject_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted-+:", 
            String.Format("test: {0}", _taintedValue), 
            () => String.Format("test: {0}", _taintedValue));
    }

    [Fact]
    public void GivenANotTaintedObject_WhenCallingFormatWithOneObject_ResultIsNotTainted()
    {
        AssertNotTainted(String.Format("test: {0}", _untaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithNewStringBuilder_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted-+:", 
            String.Format("test: {0}", new StringBuilder(_taintedValue)), 
            () => String.Format("test: {0}", new StringBuilder(_taintedValue)));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithOneObjectLess_FormatException()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => String.Format(_taintedFormat2Args, _taintedValue),
            () => String.Format(_taintedFormat2Args, _taintedValue));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithOneObject_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatww-+:",
            String.Format(_taintedFormat1Arg, "ww"),
            () => String.Format(_taintedFormat1Arg, "ww"));
    }

    // Testing public static string Format(string format, object arg0, object arg1)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithTwoObjects_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted TAINTED2-+:", 
            String.Format("test: {0} {1}", _taintedValue, _taintedValue2), 
            () => String.Format("test: {0} {1}", _taintedValue, _taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithTwoObjects_ResultIsTainted2()
    {
        string str = "{0} and Literal equals to {1}";
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted and Literal equals to UntaintedString-+:",
            String.Format(str, _taintedValue, _untaintedString),
            () => String.Format(str, _taintedValue, _untaintedString));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithTwoObjects_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatUntaintedStringTAINTED2-+:", 
            String.Format(_taintedFormat2Args, _untaintedString, _taintedValue2), 
            () => String.Format(_taintedFormat2Args, _untaintedString, _taintedValue2));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithTwoObjects_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwww-+:",
            String.Format(_taintedFormat2Args, "ww", "ww"),
            () => String.Format(_taintedFormat2Args, "ww", "ww"));
    }

    // Testing public static string Format(string format, object arg0, object arg1, object arg2)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithThreebjects_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: www www TAINTED2-+:", 
            String.Format("test: {0} {1} {2}", "www", "www", _taintedValue2), 
            () => String.Format("test: {0} {1} {2}", "www", "www", _taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithThreebjects_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: www tainted www-+:", 
            String.Format("test: {0} {1} {2}", "www", _taintedValue, "www"), 
            () => String.Format("test: {0} {1} {2}", "www", _taintedValue, "www"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithThreebjects_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted www www-+:", 
            String.Format("test: {0} {1} {2}", _taintedValue, "www", "www"), 
            () => String.Format("test: {0} {1} {2}", _taintedValue, "www", "www"));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormat_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattainted TAINTED2 TAINTED2-+:",
            String.Format("format{0} {1} {2}", _taintedValue, _taintedValue2, _taintedValue2),
            () => String.Format("format{0} {1} {2}", _taintedValue, _taintedValue2, _taintedValue2));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormat_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwwwww-+:",
            String.Format(_taintedFormat3Args, "ww", "ww", "ww"),
            () => String.Format(_taintedFormat3Args, "ww", "ww", "ww"));
    }

    // Testing public static string Format(string format, params object[] args)

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintedTAINTED2-+:", 
            String.Format(_taintedFormat2Args, new object[] { _taintedValue, _taintedValue2 }), 
            () => String.Format(_taintedFormat2Args, new object[] { _taintedValue, _taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectArray_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintednotTainted-+:", 
            String.Format(_taintedFormat2Args, new object[] { _taintedValue, "notTainted" }), 
            () => String.Format(_taintedFormat2Args, new object[] { _taintedValue, "notTainted" }));
    }

    [Fact]
    public void GivenANotTaintedFormatObject_WhenCallingFormatWithObjectArray_ResultIsNotTainted2()
    {
        AssertNotTainted(String.Format("Format{0}{1}", new object[] { "notTainted", "notTainted" }));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectArray_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-abcnotTaintedtainted-+:", 
            String.Format("abc{0}{1}", new object[] { "notTainted", _taintedValue }), 
            () => String.Format("abc{0}{1}", new object[] { "notTainted", _taintedValue }));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectArray_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatwwwwww-+:",
            String.Format(_taintedFormat3Args, new object[] { "ww", "ww", "ww" }),
            () => String.Format(_taintedFormat3Args, new object[] { "ww", "ww", "ww" }));
    }

#if NET8_0
    // System.String Format(System.IFormatProvider, System.Text.CompositeFormat, System.Object[])

    [Fact]
    public void GivenATaintedString_WhenCallingStringFormatObjectArrayFormatProvider_ResultIsTainted2()
    {
        var composite = CompositeFormat.Parse("myformat{0}{1}");
        AssertUntaintedWithOriginalCallCheck(
            "myformattaintedcustomformatUntaintedStringcustomformat",
            String.Format(new FormatProviderForTest(), composite, new object[] { _taintedValue, _untaintedString }).ToString(),
            () => String.Format(new FormatProviderForTest(), composite, new object[] { _taintedValue, _untaintedString }).ToString());
    }
#endif
}
