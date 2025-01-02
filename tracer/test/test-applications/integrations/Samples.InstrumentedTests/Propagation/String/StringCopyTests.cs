using System;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Propagation.String;
#pragma warning disable CS0618 // Obsolete

public class StringCopyTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string UntaintedString = "UntaintedString";

    public StringCopyTests()
    {
        AddTainted(taintedValue);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingCopy_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", System.String.Copy(taintedValue), () => System.String.Copy(taintedValue));
    }

    [Fact]
    public void GivenAUntaintedObject_WhenCallingCopy_ResultIsNotTainted()
    {
        AssertUntaintedWithOriginalCallCheck(() => System.String.Copy(UntaintedString), () => System.String.Copy(UntaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingCopyWithNull_ArgumentNullException()
    {
        AssertUntaintedWithOriginalCallCheck(() => System.String.Copy(null), () => System.String.Copy(null));
    }
}
#pragma warning restore CS0618 // Obsolete
