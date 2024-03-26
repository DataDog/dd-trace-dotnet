using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringBuilderPropagation;
public class StringBuilderAppendFormatTests : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";
    private string _taintedValue2 = "TAINTED2";
    private string _untaintedString = "_untaintedString";
    private string _notTaintedValue = "notTainted";
    private string _taintedString = "_taintedString";
    private string _formatTaintedValue = "format{0}{1}";
    private string _otherTaintedString = "OtherTaintedString";

    public StringBuilderAppendFormatTests()
    {
        AddTainted(_taintedValue);
        AddTainted(_taintedValue2);
        AddTainted(_taintedString);
        AddTainted(_formatTaintedValue);
        AddTainted(_otherTaintedString);
    }

    // [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.String,System.Object)")]

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatOneObjectFormatProvider_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-taintedStringBuilderLiteral equals to _untaintedString-+:",
            GetTaintedStringBuilder("taintedStringBuilder").AppendFormat("Literal equals to {0}", _untaintedString),
            () => GetTaintedStringBuilder("taintedStringBuilder").AppendFormat("Literal equals to {0}", _untaintedString));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatOneObjectFormatProvider_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-taintedStringBuilderLiteral equals to _taintedString-+:",
            GetTaintedStringBuilder("taintedStringBuilder").AppendFormat("Literal equals to {0}", _taintedString),
            () => GetTaintedStringBuilder("taintedStringBuilder").AppendFormat("Literal equals to {0}", _taintedString));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatOneObjectFormatProvider_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-taintedtainted-+:",
            new StringBuilder(_taintedValue).AppendFormat("{0}", _taintedValue).ToString(),
            () => new StringBuilder(_taintedValue).AppendFormat("{0}", _taintedValue).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatOneParamMissing_ResultIsTainted()
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(string.Empty).AppendFormat("myformat{0}{1}", _taintedValue).ToString(),
            () => new StringBuilder(string.Empty).AppendFormat("myformat{0}{1}", _taintedValue).ToString());
    }

    // [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.String,System.Object,System.Object)")]

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatTwoObjects_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-taintedStringBuilderLiteral equals to _taintedString and not to _untaintedString-+:",
            GetTaintedStringBuilder("taintedStringBuilder").AppendFormat("Literal equals to {0} and not to {1}", _taintedString, _untaintedString),
            () => GetTaintedStringBuilder("taintedStringBuilder").AppendFormat("Literal equals to {0} and not to {1}", _taintedString, _untaintedString));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatTwoObjects_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-taintedmyformattaintednotTainted-+:",
            new StringBuilder(_taintedValue).AppendFormat("myformat{0}{1}", _taintedValue, _notTaintedValue).ToString(),
            () => new StringBuilder(_taintedValue).AppendFormat("myformat{0}{1}", _taintedValue, _notTaintedValue).ToString());
    }

    // [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.String,System.Object,System.Object,System.Object)")]

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatThreeObjectFormatProvider_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-taintedStringBuilderLiteral including _taintedString and not _untaintedString and including also OtherTaintedString-+:",
            GetTaintedStringBuilder("taintedStringBuilder").AppendFormat("Literal including {0} and not {1} and including also {2}", _taintedString, _untaintedString, _otherTaintedString),
            () => GetTaintedStringBuilder("taintedStringBuilder").AppendFormat("Literal including {0} and not {1} and including also {2}", _taintedString, _untaintedString, _otherTaintedString));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatThreeObjectFormatProvider_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-taintedtaintedeeetainted-+:",
            new StringBuilder(_taintedValue).AppendFormat("{0}{1}{2}", _taintedValue, "eee", _taintedValue).ToString(),
            () => new StringBuilder(_taintedValue).AppendFormat("{0}{1}{2}", _taintedValue, "eee", _taintedValue).ToString());
    }

    // [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.String,System.Object[])")]

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatObjectArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-formattaintednotTainted-+:",
            new StringBuilder(string.Empty).AppendFormat(_formatTaintedValue, new object[] { _taintedValue, _notTaintedValue }).ToString(),
            () => new StringBuilder(string.Empty).AppendFormat(_formatTaintedValue, new object[] { _taintedValue, _notTaintedValue }).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatNottaintedObjectArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-myformattaintednotTainted-+:",
            new StringBuilder(string.Empty).AppendFormat("myformat{0}{1}", new object[] { _taintedValue, _notTaintedValue }).ToString(),
            () => new StringBuilder(string.Empty).AppendFormat("myformat{0}{1}", new object[] { _taintedValue, _notTaintedValue }).ToString());
    }

    // [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.IFormatProvider,System.String,System.Object)")]

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatOneObjectFormatProviderFormatProvider_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-taintedcustomformat-+:",
            new StringBuilder(string.Empty).AppendFormat(new FormatProviderForTest(), "{0}", _taintedValue).ToString(),
            () => new StringBuilder(string.Empty).AppendFormat(new FormatProviderForTest(), "{0}", _taintedValue).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatFormatNull_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:",
            new StringBuilder(string.Empty).AppendFormat(null, _taintedValue, _notTaintedValue).ToString(),
            () => new StringBuilder(string.Empty).AppendFormat(null, _taintedValue, _notTaintedValue).ToString());
    }

    // [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.IFormatProvider,System.String,System.Object,System.Object)")]

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatTwoObjectsFormatProvider_ResultIsTainted()
    {
        Customer customer = new Customer(AddTainted("TaintedCustomerName").ToString(), 999654);
        AssertTaintedFormatWithOriginalCallCheck(":+-taintedStringBuilder with tainted literal equals to TaintedCustomerName and number 0000-999-654-+:",
            GetTaintedStringBuilder("taintedStringBuilder").AppendFormat(new CustomerNumberFormatter(), " with tainted literal equals to {0} and number {1}", customer.Name, customer.CustomerNumber).ToString(),
            () => GetTaintedStringBuilder("taintedStringBuilder").AppendFormat(new CustomerNumberFormatter(), " with tainted literal equals to {0} and number {1}", customer.Name, customer.CustomerNumber).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatTwoObjectsFormatProvider_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-myformattaintedcustomformatnotTaintedcustomformat-+:",
            new StringBuilder(string.Empty).AppendFormat(new FormatProviderForTest(), "myformat{0}{1}", _taintedValue, _notTaintedValue).ToString(),
            () => new StringBuilder(string.Empty).AppendFormat(new FormatProviderForTest(), "myformat{0}{1}", _taintedValue, _notTaintedValue).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatOneParamNull_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-myformattainted-+:",
            new StringBuilder(string.Empty).AppendFormat("myformat{0}{1}", _taintedValue, null).ToString(),
            () => new StringBuilder(string.Empty).AppendFormat("myformat{0}{1}", _taintedValue, null).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatMoreParameters_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:",
            new StringBuilder(string.Empty).AppendFormat(null, _taintedValue, _notTaintedValue, "eee").ToString(),
            () => new StringBuilder(string.Empty).AppendFormat(null, _taintedValue, _notTaintedValue, "eee").ToString());
    }

    // [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.IFormatProvider,System.String,System.Object,System.Object,System.Object)")]

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatThreeObjectFormatProviderFormatProvider_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-taintedcustomformatTAINTED2customformattaintedcustomformat-+:",
            new StringBuilder(string.Empty).AppendFormat(new FormatProviderForTest(), "{0}{1}{2}", _taintedValue, _taintedValue2, _taintedValue).ToString(),
            () => new StringBuilder(string.Empty).AppendFormat(new FormatProviderForTest(), "{0}{1}{2}", _taintedValue, _taintedValue2, _taintedValue).ToString());
    }

    // [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.IFormatProvider,System.String,System.Object[])")]

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatObjectArrayFormatProvider_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-myformattaintedcustomformatnotTaintedcustomformat-+:", 
            new StringBuilder(string.Empty).AppendFormat(new FormatProviderForTest(), "myformat{0}{1}", new object[] { _taintedValue, _notTaintedValue }).ToString(), 
            () => new StringBuilder(string.Empty).AppendFormat(new FormatProviderForTest(), "myformat{0}{1}", new object[] { _taintedValue, _notTaintedValue }).ToString());
    }

    
#if NET8_0
    // System.StringBuilder::AppendFormat(System.IFormatProvider,System.Text.CompositeFormat,System.Object[])
     
    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendFormatObjectArrayFormatProvider_ResultIsTainted2()
    {
        var composite = CompositeFormat.Parse("myformat{0}{1}");
        AssertUntaintedWithOriginalCallCheck(
            "myformattaintedcustomformatnotTaintedcustomformat",
            new StringBuilder(string.Empty).AppendFormat(new FormatProviderForTest(), composite, new object[] { _taintedValue, _notTaintedValue }).ToString(),
            () => new StringBuilder(string.Empty).AppendFormat(new FormatProviderForTest(), composite, new object[] { _taintedValue, _notTaintedValue }).ToString());
    }
#endif
}
