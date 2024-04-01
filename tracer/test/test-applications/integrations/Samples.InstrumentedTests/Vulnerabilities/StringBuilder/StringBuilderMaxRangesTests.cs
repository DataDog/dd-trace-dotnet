using System.Text;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringBuilderPropagation;

public class StringBuilderMaxRanges : InstrumentationTestsBase
{
    private const string UntaintedString = "|";

    [Fact]
    public void GivenATaintedObject_WithNotMaxedOutRanges_WhenCallingAppend_ResultTainted()
    {
        var testString1 = AddTaintedString("1");
        var testString2 = AddTaintedString("2");
        var testString3 = AddTaintedString("3");
        var testString4 = AddTaintedString("4");
        var testString5 = AddTaintedString("5");
        var testString6 = AddTaintedString("6");
        var testString7 = AddTaintedString("7");
        var testString8 = AddTaintedString("8");
        var testString9 = AddTaintedString("9");
        var testString10 = AddTaintedString("10");

        var sb = new StringBuilder();
        sb.Append(testString1);
        sb.Append(UntaintedString);
        sb.Append(testString2);
        sb.Append(UntaintedString);
        sb.Append(testString3);
        sb.Append(UntaintedString);
        sb.Append(testString4);
        sb.Append(UntaintedString);
        sb.Append(testString5);
        sb.Append(UntaintedString);
        sb.Append(testString6);
        sb.Append(UntaintedString);
        sb.Append(testString7);
        sb.Append(UntaintedString);
        sb.Append(testString8);
        sb.Append(UntaintedString);
        sb.Append(testString9);
        sb.Append(UntaintedString);
        sb.Append(testString10);
        sb.Append(UntaintedString);

        AssertTaintedFormatWithOriginalCallCheck(":+-1-+:|:+-2-+:|:+-3-+:|:+-4-+:|:+-5-+:|:+-6-+:|:+-7-+:|:+-8-+:|:+-9-+:|:+-10-+:|",
                                                 sb.ToString(),
                                                 () => sb.ToString());
    }
    
    [Fact]
    public void GivenATaintedObject_WithMaxedOutRanges_WhenCallingAppend_ResultTainted()
    {
        var testString1 = AddTaintedString("1");
        var testString2 = AddTaintedString("2");
        var testString3 = AddTaintedString("3");
        var testString4 = AddTaintedString("4");
        var testString5 = AddTaintedString("5");
        var testString6 = AddTaintedString("6");
        var testString7 = AddTaintedString("7");
        var testString8 = AddTaintedString("8");
        var testString9 = AddTaintedString("9");
        var testString10 = AddTaintedString("10");
        var testString11 = AddTaintedString("11");

        var sb = new StringBuilder();
        sb.Append(testString1);
        sb.Append(UntaintedString);
        sb.Append(testString2);
        sb.Append(UntaintedString);
        sb.Append(testString3);
        sb.Append(UntaintedString);
        sb.Append(testString4);
        sb.Append(UntaintedString);
        sb.Append(testString5);
        sb.Append(UntaintedString);
        sb.Append(testString6);
        sb.Append(UntaintedString);
        sb.Append(testString7);
        sb.Append(UntaintedString);
        sb.Append(testString8);
        sb.Append(UntaintedString);
        sb.Append(testString9);
        sb.Append(UntaintedString);
        sb.Append(testString10);
        sb.Append(UntaintedString);
        sb.Append(testString11);
        sb.Append(UntaintedString);

        AssertTaintedFormatWithOriginalCallCheck(":+-1-+:|:+-2-+:|:+-3-+:|:+-4-+:|:+-5-+:|:+-6-+:|:+-7-+:|:+-8-+:|:+-9-+:|:+-10-+:|11|",
                                                 sb.ToString(),
                                                 () => sb.ToString());
    }
}
