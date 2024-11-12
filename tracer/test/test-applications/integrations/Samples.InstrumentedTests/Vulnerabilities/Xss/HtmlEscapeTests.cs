using System.Net;
using System.Web;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;
public class HtmlEscapeTests : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";
    private string _taintedValue2 = "<tainted>";

    public HtmlEscapeTests()
    {
        AddTainted(_taintedValue);
        AddTainted(_taintedValue2);
    }

    [Fact]
    public void GivenAValidTaintedString_WhenHttpUtilityEscaped_ResultIsTaintedWithSafeMark()
    {
        AssertSecureMarks(
            AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", 
                HttpUtility.HtmlEncode(_taintedValue), 
                () => HttpUtility.HtmlEncode(_taintedValue)),
            SecureMarks.Xss);
    }

    [Fact]
    public void GivenAnInvalidTaintedString_WhenHttpUtilityEscaped_ResultIsTaintedWithSafeMark()
    {
        AssertSecureMarks(
            AssertTaintedFormatWithOriginalCallCheck(":+-&lt;tainted&gt;-+:",
                HttpUtility.HtmlEncode(_taintedValue2),
                () => HttpUtility.HtmlEncode(_taintedValue2)),
            SecureMarks.Xss);
    }

    [Fact]
    public void GivenAValidTaintedString_WhenWebUtilityEscaped_ResultIsTaintedWithSafeMark()
    {
        AssertSecureMarks(
            AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:",
                WebUtility.HtmlEncode(_taintedValue),
                () => WebUtility.HtmlEncode(_taintedValue)),
            SecureMarks.Xss);
    }

    [Fact]
    public void GivenAnInvalidTaintedString_WhenWebUtilityEscaped_ResultIsTaintedWithSafeMark()
    {
        AssertSecureMarks(
            AssertTaintedFormatWithOriginalCallCheck(":+-&lt;tainted&gt;-+:",
                WebUtility.HtmlEncode(_taintedValue2),
                () => WebUtility.HtmlEncode(_taintedValue2)),
            SecureMarks.Xss);
    }


}
