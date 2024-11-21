#if NET6_0_OR_GREATER

using Xunit;
using System.Runtime.CompilerServices;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;

public class DefaultInterpolatedStringTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string untaintedValue = "untainted";

    public DefaultInterpolatedStringTests()
    {
        AddTainted(taintedValue);
    }
    
    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingTaintedValue_GetString_Vulnerable()
    {
        var test = new DefaultInterpolatedStringHandler(1,1);
        test.AppendFormatted("Hello");
        test.AppendFormatted(taintedValue);

        var str = test.ToStringAndClear();
        AssertTainted(str);
    }

    [Fact]
    public void GivenAnImplicitInterpolatedString_WhenAddingTaintedValue_GetString_Vulnerable()
    {
        var number = 5;
        var str = $"Hello {taintedValue} {number}";
        AssertTainted(str);
    }

    [Fact]
    public void GivenAnImplicitInterpolatedString_WhenAddingUntaintedValue_GetString_NonVulnerable()
    {
        var number = 5;
        var str = $"Hello {untaintedValue} {number}";
        AssertNotTainted(str);
    }

}

#endif
