using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;

public class StringMaxRangesTests : InstrumentationTestsBase
{
    private const string UntaintedString = "|";
    
    [Fact]
    public void GivenATaintedObject_WithAMaxedOutRanges_WhenCallingConcat_ResultIsTainted()
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
        
        AssertTaintedFormatWithOriginalCallCheck(":+-1-+:|:+-2-+:|:+-3-+:|:+-4-+:|:+-5-+:|:+-6-+:|:+-7-+:|:+-8-+:|:+-9-+:|:+-10-+:|", 
            string.Concat(testString1, UntaintedString, testString2, UntaintedString, testString3, UntaintedString, testString4, UntaintedString, testString5, UntaintedString, testString6, UntaintedString, testString7, UntaintedString, testString8, UntaintedString, testString9, UntaintedString, testString10, UntaintedString), 
            () => string.Concat(testString1, UntaintedString, testString2, UntaintedString, testString3, UntaintedString, testString4, UntaintedString, testString5, UntaintedString, testString6, UntaintedString, testString7, UntaintedString, testString8, UntaintedString, testString9, UntaintedString, testString10, UntaintedString));
    }
    
    // same but with 11 tainted strings
    [Fact]
    public void GivenATaintedObject_WithAMaxedOutRangesDefault_WhenCallingConcat_ResultTainted()
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
        var testString12 = AddTaintedString("12");

        AssertTaintedFormatWithOriginalCallCheck(":+-1-+:|:+-2-+:|:+-3-+:|:+-4-+:|:+-5-+:|:+-6-+:|:+-7-+:|:+-8-+:|:+-9-+:|:+-10-+:|11|12",
                                                 string.Concat(testString1, UntaintedString, testString2, UntaintedString, testString3, UntaintedString, testString4, UntaintedString, testString5, UntaintedString, testString6, UntaintedString, testString7, UntaintedString, testString8, UntaintedString, testString9, UntaintedString, testString10, UntaintedString, testString11, UntaintedString, testString12),
                                                 () => string.Concat(testString1, UntaintedString, testString2, UntaintedString, testString3, UntaintedString, testString4, UntaintedString, testString5, UntaintedString, testString6, UntaintedString, testString7, UntaintedString, testString8, UntaintedString, testString9, UntaintedString, testString10, UntaintedString, testString11, UntaintedString, testString12));
    }
    
    [Fact]
    public void GivenATaintedObject_WithAMaxedOutRangesDefault_WhenCallingConcatAndInsert_ResultTainted()
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
        
        AssertTaintedFormatWithOriginalCallCheck(":+-11-+::+-1-+:|:+-2-+:|:+-3-+:|:+-4-+:|:+-5-+:|:+-6-+:|:+-7-+:|:+-8-+:|:+-9-+:|10",
                                                 string.Concat(testString1, UntaintedString, testString2, UntaintedString, testString3, UntaintedString, testString4, UntaintedString, testString5, UntaintedString, testString6, UntaintedString, testString7, UntaintedString, testString8, UntaintedString, testString9, UntaintedString, testString10).Insert(0, testString11),
                                                 () => string.Concat(testString1, UntaintedString, testString2, UntaintedString, testString3, UntaintedString, testString4, UntaintedString, testString5, UntaintedString, testString6, UntaintedString, testString7, UntaintedString, testString8, UntaintedString, testString9, UntaintedString, testString10).Insert(0, testString11));
    }
}
