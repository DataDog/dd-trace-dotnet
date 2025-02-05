using System.Net;
using System.Web;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Propagation.SecurityControls;
public class SecurityControlsTests : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";

    public SecurityControlsTests()
    {
        AddTainted(_taintedValue);
    }

    public void Validate(string value)
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
        var t = _taintedValue + _taintedValue;

        Validate(_taintedValue);

        AssertSecureMarks(
            AssertTaintedFormat(":+-tainted-+:",  _taintedValue),
            SecureMarks.Xss);
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
            AssertTaintedFormat(NonSanitize(":+-tainted-+:"), _taintedValue),
            SecureMarks.None);
    }
}
