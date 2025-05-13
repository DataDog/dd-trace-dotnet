using System.Net;
using System.Web;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Propagation.SecurityControls;
public class SecurityControlsTests : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";
    private string _taintedValue1 = "tainted1";
    private string _taintedValue2 = "tainted2";
    private string _taintedValue3 = "tainted3";

    public SecurityControlsTests()
    {
        AddTainted(_taintedValue);
        AddTainted(_taintedValue1);
        AddTainted(_taintedValue2);
        AddTainted(_taintedValue3);
    }

    internal void Validate(string value)
    { 
    }

    internal void Validate(string value0, string value1, string value2, string value3)
    {
    }


    public string Sanitize(string value)
    {
        return "[" + value + "]";
    }

    public string NonSanitize(string value)
    {
        return value;
    }


    [Fact]
    public void GivenATaintedString_WhenValidating_ResultIsTaintedWithSafeMark()
    {
        Validate(_taintedValue);

        AssertSecureMarks(
            AssertTaintedFormat(":+-tainted-+:",  _taintedValue),
            SecureMarks.Xss);
    }

    [Fact]
    public void GivenATaintedString_WhenValidatingMultipleValues_ResultIsTaintedWithSafeMark()
    {
        Validate(_taintedValue, _taintedValue1, _taintedValue2, _taintedValue3);

        AssertSecureMarks(
            AssertTaintedFormat(":+-tainted-+:", _taintedValue),
            SecureMarks.Xss);

        AssertSecureMarks(
            AssertTaintedFormat(":+-tainted1-+:", _taintedValue1),
            SecureMarks.Xss);

        AssertSecureMarks(
            AssertTaintedFormat(":+-tainted2-+:", _taintedValue2),
            SecureMarks.None);

        AssertSecureMarks(
            AssertTaintedFormat(":+-tainted3-+:", _taintedValue3),
            SecureMarks.None);
    }


    [Fact]
    public void GivenATaintedString_WhenSanitizing_ResultIsTaintedWithSafeMark()
    {
        AssertSecureMarks(
            AssertTaintedFormat("[:+-tainted-+:]", Sanitize(_taintedValue)),
            SecureMarks.Xss);
    }

    [Fact]
    public void GivenATaintedString_WhenNonSanitizing_ResultIsTaintedWithNoSafeMark()
    {
        AssertSecureMarks(
            AssertTaintedFormat(":+-tainted-+:", NonSanitize(_taintedValue)),
            SecureMarks.None);
    }
}
