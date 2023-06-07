using System;
using System.Text;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;
public class StringFormatTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string taintedValue2 = "TAINTED2";
    protected string formatTaintedValue = "format{0}{1}";
    protected string UntaintedString = "UntaintedString";
    protected string OtherUntaintedString = "OtherUntaintedString";

    public StringFormatTests()
    {
        AddTainted(taintedValue);
        AddTainted(formatTaintedValue);
        AddTainted(taintedValue2);
    }

    // Testing public static string Format(IFormatProvider provider, string format, params object[] args)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted-+:", String.Format(new FormatProviderForTest(), "test: {0}", new object[] { taintedValue }), () => String.Format(new FormatProviderForTest(), "test: {0}", new object[] { taintedValue }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatUntaintedStringTAINTED2-+:", String.Format(new FormatProviderForTest(), formatTaintedValue, new object[] { UntaintedString, taintedValue2 }), () => String.Format(new FormatProviderForTest(), formatTaintedValue, new object[] { UntaintedString, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted8()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formatUntaintedStringUntaintedString-+:", String.Format(new FormatProviderForTest(), formatTaintedValue, new object[] { UntaintedString, UntaintedString }), () => String.Format(new FormatProviderForTest(), formatTaintedValue, new object[] { UntaintedString, UntaintedString }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted3()
    {
        string str = "Literal with tainteds {0}{1} and untainted {2} and tainted {3} and another untainted {4}";
        AssertTaintedFormatWithOriginalCallCheck(":+-Literal with tainteds taintedTAINTED2 and untainted UntaintedString and tainted TAINTED2 and another untainted OtherUntaintedString-+:",
            String.Format(str, taintedValue, taintedValue2, UntaintedString, taintedValue2, OtherUntaintedString),
            () => String.Format(str, taintedValue, taintedValue2, UntaintedString, taintedValue2, OtherUntaintedString));
    }

    // Testing public static string Format(IFormatProvider provider, string format, object arg0, object arg1)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintedTAINTED2-+:",
            String.Format(new FormatProviderForTest(), formatTaintedValue, taintedValue, taintedValue2),
            () => String.Format(new FormatProviderForTest(), formatTaintedValue, taintedValue, taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted5()
    {
        Customer customer = new Customer(taintedValue, 999654);
        AssertTaintedFormatWithOriginalCallCheck(":+-Tainted literal equals to tainted and number 0000-999-654-+:",
            String.Format(new CustomerNumberFormatter(), "Tainted literal equals to {0} and number {1}", customer.Name, customer.CustomerNumber),
            () => String.Format(new CustomerNumberFormatter(), "Tainted literal equals to {0} and number {1}", customer.Name, customer.CustomerNumber));
    }

    // Testing public static string Format(IFormatProvider provider, string format, object arg0)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted6()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattainted-+:",
            String.Format(new FormatProviderForTest(), "format{0}", taintedValue),
            () => String.Format(new FormatProviderForTest(), "format{0}", taintedValue));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithProvider_ResultIsTainted7()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", String.Format(null, taintedValue, taintedValue2), () => String.Format(null, taintedValue, taintedValue2));
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
        AssertTaintedFormatWithOriginalCallCheck(":+-formattainted TAINTED2 TAINTED2-+:",
            String.Format(new FormatProviderForTest(), "format{0} {1} {2}", taintedValue, taintedValue2, taintedValue2),
            () => String.Format(new FormatProviderForTest(), "format{0} {1} {2}", taintedValue, taintedValue2, taintedValue2));
    }

    // Testing public static string Format(string format, params object[] args)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithObjectArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted-+:", String.Format("test: {0}", new object[] { taintedValue }), () => String.Format("test: {0}", new object[] { taintedValue }));
    }

    // Testing public static string Format(string format, object arg0)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithOneObject_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted-+:", String.Format("test: {0}", taintedValue), () => String.Format("test: {0}", taintedValue));
    }

    [Fact]
    public void GivenANotTaintedObject_WhenCallingFormatWithOneObject_ResultIsNotTainted()
    {
        AssertNotTainted(String.Format("test: {0}", UntaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithNewStringBuilder_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted-+:", String.Format("test: {0}", new StringBuilder(taintedValue)), () => String.Format("test: {0}", new StringBuilder(taintedValue)));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithOneObjectLess_FormatException()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => String.Format(formatTaintedValue, taintedValue),
            () => String.Format(formatTaintedValue, taintedValue));
    }

    // Testing public static string Format(string format, object arg0, object arg1)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithTwoObjects_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted TAINTED2-+:", String.Format("test: {0} {1}", taintedValue, taintedValue2), () => String.Format("test: {0} {1}", taintedValue, taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithTwoObjects_ResultIsTainted2()
    {
        string str = "{0} and Literal equals to {1}";
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted and Literal equals to UntaintedString-+:",
            String.Format(str, taintedValue, UntaintedString),
            () => String.Format(str, taintedValue, UntaintedString));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithTwoObjects_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintedTAINTED2-+:", 
            String.Format(formatTaintedValue, taintedValue, taintedValue2), 
            () => String.Format(formatTaintedValue, taintedValue, taintedValue2));
    }

    // Testing public static string Format(string format, object arg0, object arg1, object arg2)

    [Fact]
    public void GivenATaintedObject_WhenCallingFormatWithThreebjects_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-test: tainted www TAINTED2-+:", String.Format("test: {0} {1} {2}", taintedValue, "www", taintedValue2), () => String.Format("test: {0} {1} {2}", taintedValue, "www", taintedValue2));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormat_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattainted TAINTED2 TAINTED2-+:",
            String.Format("format{0} {1} {2}", taintedValue, taintedValue2, taintedValue2),
            () => String.Format("format{0} {1} {2}", taintedValue, taintedValue2, taintedValue2));
    }

    // Testing public static string Format(string format, params object[] args)

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintedTAINTED2-+:", String.Format(formatTaintedValue, new object[] { taintedValue, taintedValue2 }), () => String.Format(formatTaintedValue, new object[] { taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectArray_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintednotTainted-+:", String.Format(formatTaintedValue, new object[] { taintedValue, "notTainted" }), () => String.Format(formatTaintedValue, new object[] { taintedValue, "notTainted" }));
    }

    [Fact]
    public void GivenANotTaintedFormatObject_WhenCallingFormatWithObjectArray_ResultIsNotTainted2()
    {
        AssertNotTainted(String.Format(formatTaintedValue, new object[] { "notTainted", "notTainted" }));
    }

    [Fact]
    public void GivenATaintedFormatObject_WhenCallingFormatWithObjectArray_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-abctaintednotTainted-+:", String.Format("abc{0}{1}", new object[] { taintedValue, "notTainted" }), () => String.Format("abc{0}{1}", new object[] { taintedValue, "notTainted" }));
    }
}
