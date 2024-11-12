using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;

public class StringConcatTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string taintedValue2 = "TAINTED2";
    protected string TaintedString = "TaintedString";
    protected string UntaintedString = "UntaintedString";
    protected string OtherTaintedString = "OtherTaintedString";
    protected string OtherUntaintedString = "OtherUntaintedString";
    protected object UntaintedObject = "UntaintedObject";
    protected object TaintedObject = "TaintedObject";
    protected object OtherTaintedObject = "OtherTaintedObject";
    protected object OtherUntaintedObject = "OtherUntaintedObject";    

    public StringConcatTests()
    {
        AddTainted(taintedValue);
        AddTainted(taintedValue2);
        AddTainted(TaintedObject);
        AddTainted(OtherTaintedObject);
        AddTainted(TaintedString);
        AddTainted(OtherTaintedString);
    }

    //Basic cases

    [Fact]
    public void GivenStringConcatOperations_WhenPerformed_ResultIsOK()
    {
        var testString1 = AddTaintedString("01");
        var testString2 = AddTaintedString("abc");
        var testString3 = AddTaintedString("ABCD");
        var testString4 = AddTaintedString(".,;:?");
        var testString5 = AddTaintedString("+-*/{}");

        FormatTainted(String.Concat(testString1, testString2)).Should().Be(":+-01-+::+-abc-+:");
        FormatTainted(String.Concat("01", testString2)).Should().Be("01:+-abc-+:");
        FormatTainted(String.Concat(testString1, "abc")).Should().Be(":+-01-+:abc");

        FormatTainted(String.Concat(testString1, null, testString2)).Should().Be(":+-01-+::+-abc-+:");
        FormatTainted(String.Concat((object)testString1, (object)testString2)).Should().Be(":+-01-+::+-abc-+:");
        FormatTainted(String.Concat((object)testString1, null, (object)testString2)).Should().Be(":+-01-+::+-abc-+:");

        FormatTainted(String.Concat(testString1, testString2, testString3)).Should().Be(":+-01-+::+-abc-+::+-ABCD-+:");
        FormatTainted(String.Concat((object)testString1, (object)testString2, (object)testString3)).Should().Be(":+-01-+::+-abc-+::+-ABCD-+:");

        FormatTainted(String.Concat(testString1, testString2, testString3, testString4)).Should().Be(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+:");
        FormatTainted(String.Concat((object)testString1, (object)testString2, (object)testString3, (object)testString4)).Should().Be(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+:");

        FormatTainted(String.Concat(testString1, testString2, testString3, testString4, testString5)).Should().Be(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:");
        FormatTainted(String.Concat((object)testString1, (object)testString2, (object)testString3, (object)testString4, (object)testString5)).Should().Be(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:");

        FormatTainted(String.Concat(new string[] { testString1, testString2, testString3, testString4, testString5 })).Should().Be(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:");
        FormatTainted(String.Concat(new object[] { testString1, testString2, testString3, testString4, testString5 })).Should().Be(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:");
        FormatTainted(String.Concat(testString1, " dummy ")).Should().Be(":+-01-+: dummy ");
        FormatTainted(String.Concat(" dummy ", testString2, " dummy ")).Should().Be(" dummy :+-abc-+: dummy ");
        FormatTainted(String.Concat(" dummy ", testString3)).Should().Be(" dummy :+-ABCD-+:");
        FormatTainted(String.Concat(testString1, " dummy ", testString3)).Should().Be(":+-01-+: dummy :+-ABCD-+:");

        FormatTainted(String.Concat(null, testString2, " dummy ")).Should().Be(":+-abc-+: dummy ");
        FormatTainted(String.Concat(null, testString2, (object)" dummy ")).Should().Be(":+-abc-+: dummy ");
        FormatTainted(String.Concat((object)" dummy ", null, testString2, (object)" dummy ")).Should().Be(" dummy :+-abc-+: dummy ");
    }

    [Fact]
    public void GivenStringConcatOperations_WhenPerformedWithGenerics_ResultIsOK()
    {
        var testString1 = AddTaintedString("01");
        var testString2 = AddTaintedString("abc");
        var testString3 = AddTaintedString("ABCD");
        var testString4 = AddTaintedString(".,;:?");
        var testString5 = AddTaintedString("+-*/{}");

        FormatTainted(String.Concat(new List<string> { testString1, testString2, testString3, testString4, testString5 })).Should().Be(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:");
    }

    [Fact]
    public void GivenAStringConcatOneString_WhenPerformed_ResultIsOK()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", String.Concat(taintedValue), () => String.Concat(taintedValue));
    }

    [Fact]
    public void GivenAStringConcatOneObject_WhenPerformed_ResultIsOK()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedObject-+:", String.Concat(TaintedObject), () => String.Concat(TaintedObject));
    }

    [Fact]
    public void GivenAStringConcatBasicWithTainted_WhenPerformed_ResultIsOK()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            "This is a tainted literal :+-TaintedString-+:",
            String.Concat("This is a tainted literal ", TaintedString), 
            () => String.Concat("This is a tainted literal ", TaintedString));
    }

    [Fact]
    public void GivenAStringConcatBasicWithBothTainted_WhenPerformed_ResultIsOK()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedString-+::+-TaintedString-+:",
            String.Concat(TaintedString, TaintedString),
            () => String.Concat(TaintedString, TaintedString));
    }

    [Fact]
    public void GivenAStringConcatBasicWithBoth2_WhenPerformed_ResultIsOK()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedString-+:UntaintedString",
            String.Concat(TaintedString, UntaintedString),
            () => String.Concat(TaintedString, UntaintedString));
    }

    [Fact]
    public void GivenAStringLiteralsOptimizationsConcat_WhenPerformed_ResultIsOK()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedString-+:UntaintedString",
            String.Concat(TaintedString, "UntaintedString"),
            () => String.Concat(TaintedString, "UntaintedString"));
    }

    [Fact]
    public void GivenAStringLiteralsOptimizationsConcat_WhenPerformed_ResultIsOK2()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString:+-TaintedString-+:",String.Concat("UntaintedString", TaintedString),() => String.Concat("UntaintedString", TaintedString));
    }

    [Fact]
    public void GivenAStringLiteralsOptimizationsConcat_WhenPerformed_ResultIsOK3()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString:+-TaintedString-+:","UntaintedString" + TaintedString,() => "UntaintedString" + TaintedString);
    }

    [Fact]
    public void GivenAStringConcatBasicWithBothJoined_WhenPerformed_ResultIsOK()
    {
        FormatTainted(String.Concat(String.Concat(TaintedString, UntaintedString), TaintedString)).Should().Be(":+-TaintedString-+:UntaintedString:+-TaintedString-+:");
    }

    [Fact]
    public void GivenAStringConcatThreeParamsWithBoth_WhenPerformed_ResultIsOK()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedString-+:UntaintedString:+-OtherTaintedString-+:",
            String.Concat(TaintedString, UntaintedString, OtherTaintedString),
            () => String.Concat(TaintedString, UntaintedString, OtherTaintedString));
    }

    [Fact]
    public void GivenAStringConcatThreeParamsWithUntainted_WhenPerformed_ResultIsOK()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            "UntaintedString:+-TaintedString-+:OtherUntaintedString",
            String.Concat(UntaintedString, TaintedString, OtherUntaintedString),
            () => String.Concat(UntaintedString, TaintedString, OtherUntaintedString));
    }

    [Fact]
    public void GivenAStringConcatThreeParamsWithBothJoined_WhenPerformed_ResultIsOK()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedString-+:UntaintedString:+-OtherTaintedString-+:OtherUntaintedString",
            String.Concat(str, OtherUntaintedString),
            () => String.Concat(str, OtherUntaintedString));
    }

    [Fact]
    public void GivenAStringConcatFourParam_WithBoth_WhenPerformed_ResultIsOK()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedString-+:UntaintedString:+-OtherTaintedString-+:OtherUntaintedString",
            String.Concat(TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString),
            () => String.Concat(TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString));
    }

    [Fact]
    public void GivenAStringConcatFourParamsWithUntainted_WhenPerformed_ResultIsOK()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            "UntaintedString:+-TaintedString-+:OtherUntaintedString:+-OtherTaintedString-+:",
            String.Concat(UntaintedString, TaintedString, OtherUntaintedString, OtherTaintedString),
            () => String.Concat(UntaintedString, TaintedString, OtherUntaintedString, OtherTaintedString));
    }

    [Fact]
    public void GivenAStringConcatFourParamsWithBothJoined_WhenPerformed_ResultIsOK()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedString-+:UntaintedString:+-OtherTaintedString-+:OtherUntaintedString:+-TaintedString-+:",
            String.Concat(str, TaintedString),
            () => String.Concat(str, TaintedString));
    }

    [Fact]
    public void GivenAStringConcatFourParamsWithBothJoinedInverse_WhenPerformed_ResultIsOK()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherUntaintedString, UntaintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedString-+:UntaintedStringOtherUntaintedStringUntaintedString:+-OtherTaintedString-+:",
            String.Concat(str, OtherTaintedString),
            () => String.Concat(str, OtherTaintedString));
    }

    [Fact]
    public void GivenAStringConcatFiveParamsWithBothJoinedWhenPerformed_ResultIsOK()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherUntaintedString, OtherTaintedString, OtherTaintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedString-+:UntaintedStringOtherUntaintedString:+-OtherTaintedString-+::+-OtherTaintedString-+:OtherUntaintedString",
            String.Concat(str, OtherUntaintedString),
            () => (String.Concat(str, OtherUntaintedString)));
    }

    [Fact]
    public void GivenAStringConcatFiveParamsWithBothJoinedInverse_WhenPerformed_ResultIsOK()
    {
        string str = String.Concat(UntaintedString, OtherUntaintedString, TaintedString, OtherTaintedString, OtherUntaintedString);
        AssertTaintedFormatWithOriginalCallCheck(
            "UntaintedStringOtherUntaintedString:+-TaintedString-+::+-OtherTaintedString-+:OtherUntaintedStringOtherUntaintedString",
            String.Concat(str, OtherUntaintedString),
            () => String.Concat(str, OtherUntaintedString));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWith2StringParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("concat:+-tainted-+:", String.Concat("concat", taintedValue), () => String.Concat("concat", taintedValue));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWith2StringParams_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:concat", String.Concat(taintedValue, "concat"), () => String.Concat(taintedValue, "concat"));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWith3StringParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+:concat:+-tainted-+:", String.Concat(taintedValue2, "concat", taintedValue), () => String.Concat(taintedValue2, "concat", taintedValue));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWith4Params_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+:concat:+-tainted-+:CONCAT2", String.Concat(taintedValue2, "concat", taintedValue, "CONCAT2"), () => String.Concat(taintedValue2, "concat", taintedValue, "CONCAT2"));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWithStringNullParam_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("concatCONCAT2:+-TAINTED2-+:", String.Concat("concat", "CONCAT2", null, taintedValue2), () => String.Concat("concat", "CONCAT2", null, taintedValue2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcat_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", String.Concat(taintedValue), () => String.Concat(taintedValue));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcat_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", String.Concat(null, taintedValue), () => String.Concat(null, taintedValue));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcat_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", String.Concat(taintedValue, null), () => String.Concat(taintedValue, null));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcat_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-tainted-+:", String.Concat(taintedValue, taintedValue, null), () => String.Concat(taintedValue, taintedValue, null));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcat_ResultIsTainted5()
    {
        AssertTaintedFormatWithOriginalCallCheck($"string :+-tainted-+: and :+-tainted-+:", $"string {taintedValue} and {taintedValue}", () => $"string {taintedValue} and {taintedValue}");
    }

    [Fact]
    public void GivenANullString_WhenCallingConcat_ResultIsEmpty()
    {
        Assert.Throws<ArgumentNullException>(() => String.Concat(null));
    }

    [Fact]
    public void Given2NullStrings_WhenCallingConcat_ResultIsEmpty()
    {
        String.Concat(null, null).Should().BeEmpty();
    }

    //Literals

    [Fact]
    public void GivenStringLiteralsOptimizations_WhenConcat_ResultIsNotTainted()
    {
        AssertNotTainted(String.Concat("Literal1", "Literal2"));
    }

    [Fact]
    public void GivenStringLiteralsOptimizations_WhenConcat3Literals_ResultIsNotTainted()
    {
        AssertNotTainted(String.Concat("Literal1", "Literal2", "Literal3"));
    }

    [Fact]
    public void GivenStringLiteralsOptimizations_WhenConcat4Literals_ResultIsNotTainted()
    {
        AssertNotTainted(String.Concat("Literal1", "Literal2", "Literal3", "Literal4"));
    }

    [Fact]
    public void GivenStringLiteralsOptimizations_WhenConcat5Literals_ResultIsNotTainted()
    {
        AssertNotTainted(String.Concat("Literal1", "Literal2", "Literal3", "Literal4", "Literal5"));
    }

    [Fact]
    public void GivenString_WhenConcatChainedLiterals_ResultIsNotTainted()
    {
        AssertNotTainted("Literal1" + "Literal2".ToLower() + "Literal3".ToUpper() + "Literal3".Trim());
    }

    // Lambdas

    [Fact]
    public void GivenStringLambda_WhenConcat_ResultIsTainted()
    {
        var values = new List<string> { TaintedString, UntaintedString };
        string result = string.Empty;
        values.ForEach(x => result += x);
        FormatTainted(result).Should().Be(":+-TaintedString-+:UntaintedString");
    }

    [Fact]
    public void GivenStringLambda_WhenConcat_ResultIsTainted2()
    {
        var values = new List<string> { TaintedString, UntaintedString };
        string result = TaintedString;
        values.ForEach(x => result += x);
        FormatTainted(result).Should().Be(":+-TaintedString-+::+-TaintedString-+:UntaintedString");
    }

    // Objects

    [Fact]
    public void GivenStringConcatObjectWithTainted_WhenConcat_ResultISTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            "This is a tainted literal :+-TaintedObject-+:",
            String.Concat("This is a tainted literal ", TaintedObject),
            () => String.Concat("This is a tainted literal ", TaintedObject));
    }

    [Fact]
    public void GivenStringConcatObjectWithBoth_WhenConcat_ResultISTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedObject-+:UntaintedObject",
            String.Concat(TaintedObject, UntaintedObject),
            () => String.Concat(TaintedObject, UntaintedObject));
    }

    [Fact]
    public void GivenStringConcatObjectWithBothJoined_WhenConcat_ResultISTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedObject-+:UntaintedObject:+-TaintedObject-+:",
            String.Concat(String.Concat(TaintedObject, UntaintedObject), TaintedObject),
            () => String.Concat(String.Concat(TaintedObject, UntaintedObject), TaintedObject));
    }

    [Fact]
    public void GivenStringConcatObjectThreeParamsWithBoth_WhenConcat_ResultISTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedObject-+:UntaintedObject:+-OtherTaintedObject-+:",
            String.Concat(TaintedObject, UntaintedObject, OtherTaintedObject),
            () => String.Concat(TaintedObject, UntaintedObject, OtherTaintedObject));
    }

    [Fact]
    public void GivenStringConcatObjectThreeParamsWithUntainted_WhenConcat_ResultISTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            "UntaintedObject:+-TaintedObject-+:OtherUntaintedObject",
            String.Concat(UntaintedObject, TaintedObject, OtherUntaintedObject),
            () => String.Concat(UntaintedObject, TaintedObject, OtherUntaintedObject));
    }

    [Fact]
    public void GivenStringConcatObjectThreeParamsWithBothJoined_WhenConcat_ResultIsTainted()
    {
        string str = String.Concat(TaintedObject, UntaintedObject, OtherTaintedObject);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedObject-+:UntaintedObject:+-OtherTaintedObject-+:OtherUntaintedObject",
            String.Concat(str, OtherUntaintedObject),
            () => String.Concat(str, OtherUntaintedObject));
    }

    [Fact]
    public void GivenStringConcatObjectFourParamsWithBoth_WhenConcat_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedObject-+:UntaintedObject:+-OtherTaintedObject-+:OtherUntaintedObject",
            String.Concat(TaintedObject, UntaintedObject, OtherTaintedObject, OtherUntaintedObject),
            () => String.Concat(TaintedObject, UntaintedObject, OtherTaintedObject, OtherUntaintedObject));

    }

    [Fact]
    public void GivenStringConcatObjectFourParamsWithUntainted_WhenConcat_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            "UntaintedObject:+-TaintedObject-+:OtherUntaintedObject:+-OtherTaintedObject-+:",
            String.Concat(UntaintedObject, TaintedObject, OtherUntaintedObject, OtherTaintedObject),
            () => String.Concat(UntaintedObject, TaintedObject, OtherUntaintedObject, OtherTaintedObject));
    }

    [Fact]
    public void GivenStringConcatObjectFourParamsWithBothJoined_WhenConcat_ResultIsTainted()
    {
        string str = String.Concat(TaintedObject, UntaintedObject, OtherTaintedObject, OtherUntaintedObject);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedObject-+:UntaintedObject:+-OtherTaintedObject-+:OtherUntaintedObject:+-TaintedObject-+:",
            String.Concat(str, TaintedObject),
            () => String.Concat(str, TaintedObject));
    }

    [Fact]
    public void GivenStringConcatObjectFourParamsWithBothJoinedInverse_WhenConcat_ResultIsTainted()
    {
        string str = String.Concat(TaintedObject, UntaintedObject, OtherUntaintedObject, UntaintedObject);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedObject-+:UntaintedObjectOtherUntaintedObjectUntaintedObject:+-OtherTaintedObject-+:",
            String.Concat(str, OtherTaintedObject),
            () => String.Concat(str, OtherTaintedObject));
    }

    [Fact]
    public void GivenStringConcatObjectFiveParamsWithBothJoined_WhenConcat_ResultIsTainted()
    {
        string str = String.Concat(TaintedObject, UntaintedObject, OtherUntaintedObject, OtherTaintedObject, OtherTaintedObject);
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedObject-+:UntaintedObjectOtherUntaintedObject:+-OtherTaintedObject-+::+-OtherTaintedObject-+:OtherUntaintedObject",
            String.Concat(str, OtherUntaintedObject),
            () => String.Concat(str, OtherUntaintedObject));
    }

    [Fact]
    public void GivenStringConcatObjectFiveParamsWithBothJoinedInverse_WhenConcat_ResultIsTainted()
    {
        string str = String.Concat(UntaintedObject, OtherUntaintedObject, TaintedObject, OtherTaintedObject, OtherUntaintedObject);
        AssertTaintedFormatWithOriginalCallCheck(
            "UntaintedObjectOtherUntaintedObject:+-TaintedObject-+::+-OtherTaintedObject-+:OtherUntaintedObjectOtherUntaintedObject",
            String.Concat(str, OtherUntaintedObject), 
            () => String.Concat(str, OtherUntaintedObject));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingConcatWith2ObjectParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("concat:+-tainted-+:", String.Concat((object)"concat", taintedValue), () => String.Concat((object)"concat", taintedValue));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingConcatWith2ObjectParams_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck("concat:+-tainted-+:", String.Concat("concat", (object)taintedValue), () => String.Concat("concat", (object)taintedValue));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingConcatWith3ObjectParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+:concat:+-tainted-+:", String.Concat(taintedValue2, (object)"concat", taintedValue), () => String.Concat(taintedValue2, (object)"concat", taintedValue));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingConcatWith4ObjectParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+:concat:+-tainted-+:concat2", String.Concat(taintedValue2, (object)"concat", taintedValue, (object)"concat2"), () => String.Concat(taintedValue2, (object)"concat", taintedValue, (object)"concat2"));
    }

    [Fact]
    public void GivenAnObjectList_WhenConcat_ResultIsOk()
    {
        AssertTaintedFormatWithOriginalCallCheck("123:+-TaintedObject-+:", String.Concat<object>(new List<object> { 1, 2, 3, TaintedObject }), () => String.Concat<object>(new List<object> { 1, 2, 3, TaintedObject }));
    }

    // structs and built-in types

    [Fact]
    public void Given_StringConcatGenericStruct_WhenConcat_ResultIsTainted()
    {
        string str = String.Concat<StructForStringTest>(new List<StructForStringTest> { new StructForStringTest(UntaintedString), new StructForStringTest(TaintedString) });
        FormatTainted(str).Should().Be("UntaintedString:+-TaintedString-+:");
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingConcat_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString:+-tainted-+:", String.Concat<StructForStringTest>(new List<StructForStringTest> { new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue) }), () => String.Concat<StructForStringTest>(new List<StructForStringTest> { new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue) }));
    }

    [Fact]
    public void GivenANullStruct_WhenCallingConcat_ResultIsEmpty()
    {
        String.Concat<StructForStringTest?>(new List<StructForStringTest?> { null }).Should().Be(string.Empty);
    }

    [Fact]
    public void GivenAnIntList_WhenConcat_ResultIsOk()
    {
        string str = String.Concat<int>(new List<int> { 1, 2, 3});
        str.Should().Be("123");
    }

    [Fact]
    public void GivenAnIntList_WhenConcat_ResultIsOk2()
    {
        string str = String.Concat<int?>(new List<int?> { 1, 2, null, 3 });
        str.Should().Be("123");
    }

    [Fact]
    public void GivenAnCharList_WhenConcat_ResultIsOk2()
    {
        string str = String.Concat<char?>(new List<char?> { '1', '2', null, '3' });
        str.Should().Be("123");
    }

    // Classes

    [Fact]
    public void GivenATaintedStringInClassList_WhenCallingConcat_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString:+-TaintedString-+:",
            String.Concat<ClassForStringTest>(new List<ClassForStringTest> { new ClassForStringTest(UntaintedString), new ClassForStringTest(TaintedString) }),
            () => String.Concat<ClassForStringTest>(new List<ClassForStringTest> { new ClassForStringTest(UntaintedString), new ClassForStringTest(TaintedString) }));
    }

    [Fact]
    public void GivenATaintedStringInClassArray_WhenCallingConcat_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString:+-tainted-+:", 
            String.Concat<ClassForStringTest>(new ClassForStringTest[] { new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue) }), 
            () => String.Concat<ClassForStringTest>(new List<ClassForStringTest> { new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue) }));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingConcat_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString:+-tainted-+:", String.Concat<ClassForStringTest>(new List<ClassForStringTest> { new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue) }), () => String.Concat<ClassForStringTest>(new List<ClassForStringTest> { new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue) }));
    }

    [Fact]
    public void GivenANullInList_WhenCallingConcat_ResultIsTainted4()
    {
        String.Concat<ClassForStringTest>(new List<ClassForStringTest> { null }).Should().Be(string.Empty);
    }

    // string enumerables 

    [Fact]
    public void GivenATaintedObject_WhenCallingConcatWithStringArrayParam_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("concatCONCAT2:+-tainted-+::+-TAINTED2-+:", String.Concat(new string[] { "concat", "CONCAT2", taintedValue, taintedValue2 }), () => String.Concat(new string[] { "concat", "CONCAT2", taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWithStringIEnumerableStringParam_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("concatCONCAT2:+-tainted-+::+-TAINTED2-+:", String.Concat(new List<string> { "concat", "CONCAT2", taintedValue, taintedValue2 }), () => String.Concat(new List<string> { "concat", "CONCAT2", taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWithStringIEnumerableNullParam_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("concatCONCAT2:+-TAINTED2-+:", String.Concat(new List<string> { "concat", "CONCAT2", null, taintedValue2 }), () => String.Concat(new List<string> { "concat", "CONCAT2", null, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWithStringIEnumerableStringParam_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck("concatCONCAT2:+-tainted-+::+-TAINTED2-+:", String.Concat<string>(new List<string> { "concat", "CONCAT2", taintedValue, taintedValue2 }), () => String.Concat<string>(new List<string> { "concat", "CONCAT2", taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWithStringArrayNullParam_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("concatCONCAT2:+-TAINTED2-+:", String.Concat(new string[] { "concat", "CONCAT2", null, taintedValue2 }), () => String.Concat(new string[] { "concat", "CONCAT2", null, taintedValue2 }));
    }

    // object enumerables

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWithObjectArrayParam_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("concatconcat2:+-tainted-+::+-TAINTED2-+:", String.Concat(new object[] { "concat", "concat2", taintedValue, taintedValue2 }), () => String.Concat(new object[] { "concat", "concat2", taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingConcatWithObjectListParam_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("concatconcat2:+-tainted-+::+-TAINTED2-+:", String.Concat(new List<object> { "concat", "concat2", taintedValue, taintedValue2 }), () => String.Concat(new List<object> { "concat", "concat2", taintedValue, taintedValue2 }));
    }

    [Fact]

    public void GivenATaintedObject_WhenCallingConcatWithGenericObjectArrayParam_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("concatconcat2:+-tainted-+::+-TAINTED2-+:", String.Concat<object>(new object[] { "concat", "concat2", taintedValue, taintedValue2 }), () => String.Concat<object>(new object[] { "concat", "concat2", taintedValue, taintedValue2 }));
    }

    [Fact]

    public void GivenATaintedObject_WhenCallingConcatWithGenericObjectListParam_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("concatconcat2:+-tainted-+::+-TAINTED2-+:", String.Concat<object>(new List<object> { "concat", "concat2", taintedValue, taintedValue2 }), () => String.Concat<object>(new List<object> { "concat", "concat2", taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenAListOfObjects_WhenCallingConcat_ThenNoExceptionIsThrown()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:4str2", String.Concat(taintedValue, 4, null, "str2"), () => String.Concat(taintedValue, 4, null, "str2"));
    }

    [Fact]
    public void GivenAListOfStrings_WhenCallingConcat_ThenNoExceptionIsThrown()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:str2", String.Concat(taintedValue, null, "str2"), () => String.Concat(taintedValue, null, "str2"));
    }

    [Fact]
    public void GivenAnArrayOfObjectsWithFirstNullArgument_WhenCalling_Concat_ResultIsOk()
    {
        string testString1 = AddTaintedString("01");
        string testString2 = AddTaintedString("abc");
        string testString3 = AddTaintedString("ABCD");

        object[] obj = new object[4];

        obj[0] = null;
        obj[1] = testString1;
        obj[2] = testString2;
        obj[3] = testString3;

        AssertTaintedFormatWithOriginalCallCheck(":+-01-+::+-abc-+::+-ABCD-+:", String.Concat(obj), () => String.Concat(obj));
    }

    [Fact]
    public void GivenAnArrayOfObjectsWithOneNullArgument_WhenCalling_Concat_ResultIsOk()
    {
        string testString1 = AddTaintedString("01");
        string testString2 = AddTaintedString("abc");
        string testString3 = AddTaintedString("ABCD");

        object[] obj = new object[4];

        obj[0] = testString2;
        obj[1] = testString1;
        obj[2] = null;
        obj[3] = testString3;

        AssertTaintedFormatWithOriginalCallCheck(":+-abc-+::+-01-+::+-ABCD-+:", String.Concat(obj), () => String.Concat(obj));
    }

    [Fact]
    public void GivenAnArrayOfObjectsWithOneNotNullArgument_WhenCalling_Concat_ResultIsArgument()
    {
        string testString1 = AddTaintedString("01");

        object[] obj = new object[4];

        obj[0] = testString1;
        obj[1] = null;
        obj[2] = null;
        obj[3] = null;

        AssertTaintedFormatWithOriginalCallCheck(":+-01-+:", String.Concat(obj), () => String.Concat(obj));
    }

    [Fact]
    public void GivenAnArrayOfObjectsWithAllNullArguments_WhenCalling_Concat_ResultIsEmpty()
    {
        object[] obj = new object[4];

        obj[0] = null;
        obj[1] = null;
        obj[2] = null;
        obj[3] = null;

        String.Concat(obj).Should().BeEmpty();
    }
}

