#if NET6_0_OR_GREATER

using Xunit;
using System.Runtime.CompilerServices;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;

public class DefaultInterpolatedStringTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";

    public DefaultInterpolatedStringTests()
    {
        AddTainted(taintedValue);
    }
    
    [Fact]
    public void GivenAnInterpolatedString_WhenInterpolatingString_GetString_Vulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted("Hello");
        test.AppendFormatted(taintedValue);

        var str = test.ToStringAndClear();
        AssertTainted(str);
    }
}

#endif
