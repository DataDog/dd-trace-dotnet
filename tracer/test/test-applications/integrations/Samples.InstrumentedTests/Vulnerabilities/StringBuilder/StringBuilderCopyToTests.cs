using System.Text;
using Xunit;
namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringBuilderPropagation;

public class StringBuilderCopyToTests : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";

    public StringBuilderCopyToTests()
    {
        AddTainted(_taintedValue);
    }

    [Fact]
    public void GivenATaintedString_WhenCallingCopyTo_ResultIsTainted()
    {
        char[] result = new char[20];
        new StringBuilder(_taintedValue).CopyTo(0, result, 0, 3);
        AssertTainted(result);
    }

    [Theory]
    [InlineData(-1, 0, 3)]
    [InlineData(1000, 0, 3)]
    [InlineData(1, -1, 3)]
    [InlineData(1, 1000, 3)]
    [InlineData(1, 0, -3)]
    [InlineData(1, 0, 1000)]
    public void GivenATaintedString_WhenCallingCopyToWrongArguments_ArgumentOutOfRangeException(int sourceIndex, int destinationIndex, int count)
    {
        char[] result = new char[20];
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).CopyTo(sourceIndex, result, destinationIndex, count), 
            () => new StringBuilder(_taintedValue).CopyTo(sourceIndex, result, destinationIndex, count));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingCopyToWrongArguments_ArgumentNullException()
    {
        char[] result = null;

        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).CopyTo(0, result, 1, 1),
            () => new StringBuilder(_taintedValue).CopyTo(0, result, 1, 1));
    }
}

