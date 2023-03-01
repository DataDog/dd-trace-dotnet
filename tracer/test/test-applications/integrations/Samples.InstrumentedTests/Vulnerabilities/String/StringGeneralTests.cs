using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;
public class StringGeneralTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    public StringGeneralTests()
    {
        AddTainted(taintedValue);
    }

    [Fact]
    public void GivenString_WhenToString_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", taintedValue.ToString(), () => taintedValue.ToString());
    }
}
