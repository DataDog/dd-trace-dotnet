using System;
using System.Net;
using System.Text;
using System.Web;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;
public class UrlEscapeTests : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";
    private string _taintedValue2 = "/tainted?p1=t1&p2=t2";
    private string _taintedFormat2Args = "format{0}{1}";
    private string _taintedFormat3Args = "format{0}{1}{2}";
    private string _taintedFormat1Arg = "format{0}";
    private string _untaintedString = "UntaintedString";
    private string _otherUntaintedString = "OtherUntaintedString";

    public UrlEscapeTests()
    {
        AddTainted(_taintedValue);
        AddTainted(_taintedValue2);
    }

    [Fact]
    public void GivenAValidTaintedString_WhenHttpUtilityEscaped_ResultIsTaintedWithSafeMark()
    {
        AssertSecureMarks(
            AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", 
                HttpUtility.UrlEncode(_taintedValue), 
                () => HttpUtility.HtmlEncode(_taintedValue)),
            SecureMarks.Ssrf);
    }

    [Fact]
    public void GivenAnInvalidTaintedString_WhenHttpUtilityEscaped_ResultIsTaintedWithSafeMark()
    {
        AssertSecureMarks(
            AssertTaintedFormatWithOriginalCallCheck(":+-%2ftainted%3fp1%3dt1%26p2%3dt2-+:",
                HttpUtility.UrlEncode(_taintedValue2),
                () => HttpUtility.UrlEncode(_taintedValue2)),
            SecureMarks.Ssrf);
    }

    [Fact]
    public void GivenAnInvalidTaintedString_WhenHttpUtilityEscapedUTF8_ResultIsTaintedWithSafeMark1()
    {
        AssertSecureMarks(
            AssertTaintedFormatWithOriginalCallCheck(":+-%2ftainted%3fp1%3dt1%26p2%3dt2-+:",
                HttpUtility.UrlEncode(_taintedValue2, System.Text.Encoding.UTF8),
                () => HttpUtility.UrlEncode(_taintedValue2)),
            SecureMarks.Ssrf);
    }

    [Fact]
    public void GivenAValidTaintedString_WhenWebUtilityEscaped_ResultIsTaintedWithSafeMark()
    {
        AssertSecureMarks(
            AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:",
                WebUtility.UrlEncode(_taintedValue),
                () => WebUtility.UrlEncode(_taintedValue)),
            SecureMarks.Ssrf);
    }

    [Fact]
    public void GivenAnInvalidTaintedString_WhenWebUtilityEscaped_ResultIsTaintedWithSafeMark()
    {
        AssertSecureMarks(
            AssertTaintedFormatWithOriginalCallCheck(":+-%2Ftainted%3Fp1%3Dt1%26p2%3Dt2-+:",
                WebUtility.UrlEncode(_taintedValue2),
                () => WebUtility.UrlEncode(_taintedValue2)),
            SecureMarks.Ssrf);
    }

}
