using System;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;
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
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", String.Copy(taintedValue), () => String.Copy(taintedValue));
    }

    [Fact]
    public void GivenAUntaintedObject_WhenCallingCopy_ResultIsNotTainted()
    {
        AssertUntaintedWithOriginalCallCheck(() => String.Copy(UntaintedString), () => String.Copy(UntaintedString));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingCopyWithNull_ArgumentNullException()
    {
        AssertUntaintedWithOriginalCallCheck(() => String.Copy(null), () => String.Copy(null));
    }
}
#pragma warning restore CS0618 // Obsolete
