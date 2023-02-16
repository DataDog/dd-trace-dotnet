using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;

public class StringAspectTests : InstrumentationTestsBase
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

    public StringAspectTests()
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
    public void GivenStringConcatBasicOperations_WhenPerformed_ResultIsOK()
    {
        string testString1 = (string) AddTainted("01");
        string testString2 = (string) AddTainted("abc");
        string testString3 = (string) AddTainted("ABCD");
        string testString4 = (string) AddTainted(".,;:?");
        string testString5 = (string) AddTainted("+-*/{}");

        Assert.Equal(":+-01-+::+-abc-+:", FormatTainted(String.Concat(testString1, testString2)));
        Assert.Equal(":+-01-+::+-abc-+:", FormatTainted(String.Concat(testString1, null, testString2)));
        Assert.Equal(":+-01-+::+-abc-+:", FormatTainted(String.Concat((object)testString1, (object)testString2)));
        Assert.Equal(":+-01-+::+-abc-+:", FormatTainted(String.Concat((object)testString1, null, (object)testString2)));

        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+:", FormatTainted(String.Concat(testString1, testString2, testString3)));
        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+:", FormatTainted(String.Concat((object)testString1, (object)testString2, (object)testString3)));

        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+:", FormatTainted(String.Concat(testString1, testString2, testString3, testString4)));
        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+:", FormatTainted(String.Concat((object)testString1, (object)testString2, (object)testString3, (object)testString4)));

        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:", FormatTainted(String.Concat(testString1, testString2, testString3, testString4, testString5)));
        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:", FormatTainted(String.Concat((object)testString1, (object)testString2, (object)testString3, (object)testString4, (object)testString5)));

        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:", FormatTainted(String.Concat(new string[] { testString1, testString2, testString3, testString4, testString5 })));
        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:", FormatTainted(String.Concat(new object[] { testString1, testString2, testString3, testString4, testString5 })));
        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:", FormatTainted(String.Concat(new List<string> { testString1, testString2, testString3, testString4, testString5 })));
        Assert.Equal(":+-01-+: dummy ", FormatTainted(String.Concat(testString1, " dummy ")));
        Assert.Equal(" dummy :+-abc-+: dummy ", FormatTainted(String.Concat(" dummy ", testString2, " dummy ")));
        Assert.Equal(" dummy :+-ABCD-+:", FormatTainted(String.Concat(" dummy ", testString3)));
        Assert.Equal(":+-01-+: dummy :+-ABCD-+:", FormatTainted(String.Concat(testString1, " dummy ", testString3)));

        Assert.Equal(":+-abc-+: dummy ", FormatTainted(String.Concat(null, testString2, " dummy ")));
        Assert.Equal(":+-abc-+: dummy ", FormatTainted(String.Concat(null, testString2, (object)" dummy ")));
        Assert.Equal(" dummy :+-abc-+: dummy ", FormatTainted(String.Concat((object)" dummy ", null, testString2, (object)" dummy ")));
    }

    [Fact]
    public void GivenAStringConcatBasicWithTainted_WhenPerformed_ResultIsOK()
    {
        Assert.Equal("This is a tainted literal :+-TaintedString-+:", FormatTainted(String.Concat("This is a tainted literal ", TaintedString)));
    }

    [Fact]
    public void GivenAStringConcatBasicWithBothTainted_WhenPerformed_ResultIsOK()
    {
        Assert.Equal(":+-TaintedString-+::+-TaintedString-+:", FormatTainted(String.Concat(TaintedString, TaintedString)));
    }

    [Fact]
    public void GivenAStringConcatBasicWithBoth2_WhenPerformed_ResultIsOK()
    {
        Assert.Equal(":+-TaintedString-+:UntaintedString", FormatTainted(String.Concat(TaintedString, UntaintedString)));
    }

    [Fact]
    public void GivenAStringLiteralsOptimizationsConcat_WhenPerformed_ResultIsOK()
    {
        Assert.Equal(":+-TaintedString-+:UntaintedString", FormatTainted(String.Concat(TaintedString, "UntaintedString")));
    }

    [Fact]
    public void GivenAStringLiteralsOptimizationsConcat_WhenPerformed_ResultIsOK2()
    {
        Assert.Equal("UntaintedString:+-TaintedString-+:", FormatTainted(String.Concat("UntaintedString", TaintedString)));
    }

    [Fact]
    public void GivenAStringLiteralsOptimizationsConcat_WhenPerformed_ResultIsOK3()
    {
        Assert.Equal("UntaintedString:+-TaintedString-+:", FormatTainted("UntaintedString" + TaintedString));
    }

    [Fact]
    public void GivenAStringConcatBasicWithBothJoined_WhenPerformed_ResultIsOK()
    {
        Assert.Equal(":+-TaintedString-+:UntaintedString:+-TaintedString-+:", FormatTainted(String.Concat(String.Concat(TaintedString, UntaintedString), TaintedString)));
    }

    [Fact]
    public void GivenAStringConcatThreeParamsWithBoth_WhenPerformed_ResultIsOK()
    {
        Assert.Equal(":+-TaintedString-+:UntaintedString:+-OtherTaintedString-+:", FormatTainted(String.Concat(TaintedString, UntaintedString, OtherTaintedString)));
    }

    [Fact]
    public void GivenAStringConcatThreeParamsWithUntainted_WhenPerformed_ResultIsOK()
    {
        Assert.Equal("UntaintedString:+-TaintedString-+:OtherUntaintedString", FormatTainted(String.Concat(UntaintedString, TaintedString, OtherUntaintedString)));
    }

    [Fact]
    public void GivenAStringConcatThreeParamsWithBothJoined_WhenPerformed_ResultIsOK()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        Assert.Equal(":+-TaintedString-+:UntaintedString:+-OtherTaintedString-+:OtherUntaintedString", FormatTainted(String.Concat(str, OtherUntaintedString)));
    }

    [Fact]
    public void GivenAStringConcatFourParam_WithBoth_WhenPerformed_ResultIsOK()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString);
        Assert.Equal(":+-TaintedString-+:UntaintedString:+-OtherTaintedString-+:OtherUntaintedString", FormatTainted(str));
    }

    [Fact]
    public void GivenAStringConcatFourParamsWithUntainted_WhenPerformed_ResultIsOK()
    {
        string str = String.Concat(UntaintedString, TaintedString, OtherUntaintedString, OtherTaintedString);
        Assert.Equal("UntaintedString:+-TaintedString-+:OtherUntaintedString:+-OtherTaintedString-+:", FormatTainted(str));
    }

    [Fact]
    public void GivenAStringConcatFourParamsWithBothJoined_WhenPerformed_ResultIsOK()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString);
        Assert.Equal(":+-TaintedString-+:UntaintedString:+-OtherTaintedString-+:OtherUntaintedString:+-TaintedString-+:", FormatTainted(String.Concat(str, TaintedString)));
    }

    [Fact]
    public void GivenAStringConcatFourParamsWithBothJoinedInverse_WhenPerformed_ResultIsOK()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherUntaintedString, UntaintedString);
        Assert.Equal(":+-TaintedString-+:UntaintedStringOtherUntaintedStringUntaintedString:+-OtherTaintedString-+:", FormatTainted(String.Concat(str, OtherTaintedString)));
    }

    [Fact]
    public void GivenAStringConcatFiveParamsWithBothJoinedWhenPerformed_ResultIsOK()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherUntaintedString, OtherTaintedString, OtherTaintedString);
        Assert.Equal(":+-TaintedString-+:UntaintedStringOtherUntaintedString:+-OtherTaintedString-+::+-OtherTaintedString-+:OtherUntaintedString", FormatTainted(String.Concat(str, OtherUntaintedString)));
    }

    [Fact]
    public void GivenAStringConcatFiveParamsWithBothJoinedInverse_WhenPerformed_ResultIsOK()
    {
        string str = String.Concat(UntaintedString, OtherUntaintedString, TaintedString, OtherTaintedString, OtherUntaintedString);
        Assert.Equal("UntaintedStringOtherUntaintedString:+-TaintedString-+::+-OtherTaintedString-+:OtherUntaintedStringOtherUntaintedString", FormatTainted(String.Concat(str, OtherUntaintedString)));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWith2StringParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("concat:+-tainted-+:", String.Concat("concat", taintedValue), () => String.Concat("concat", taintedValue));
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
    public void GivenANullString_WhenCallingConcat_ResultIsEmpty()
    {
        try
        {
            String.Concat(null);
            throw new XunitException("No ArgumentNullException was thrown");
        }
        catch(Exception ex)
        {
            ex.GetType().Should().Be(typeof(ArgumentNullException));
        }
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
        AssertNotTainted(String.Concat("Literal1", "TaintedString"));
    }

    [Fact]
    public void GivenStringLiteralsOptimizations_WhenConcat2Literals_ResultIsNotTainted()
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
        Assert.Equal(":+-TaintedString-+:UntaintedString", FormatTainted(result));
    }

    [Fact]
    public void GivenStringLambda_WhenConcat_ResultIsTainted2()
    {
        var values = new List<string> { TaintedString, UntaintedString };
        string result = TaintedString;
        values.ForEach(x => result += x);
        Assert.Equal(":+-TaintedString-+::+-TaintedString-+:UntaintedString", FormatTainted(result));
    }

    // Objects

    [Fact]
    public void GivenStringConcatObjectWithTainted_WhenConcat_ResultISTainted()
    {
        Assert.Equal("This is a tainted literal :+-TaintedObject-+:", FormatTainted(String.Concat("This is a tainted literal ", TaintedObject)));
    }

    [Fact]
    public void GivenStringConcatObjectWithBoth_WhenConcat_ResultISTainted()
    {
        Assert.Equal(":+-TaintedObject-+:UntaintedObject", FormatTainted(String.Concat(TaintedObject, UntaintedObject)));
    }

    [Fact]
    public void GivenStringConcatObjectWithBothJoined_WhenConcat_ResultISTainted()
    {
        Assert.Equal(":+-TaintedObject-+:UntaintedObject:+-TaintedObject-+:", FormatTainted(String.Concat(String.Concat(TaintedObject, UntaintedObject), TaintedObject)));
    }

    [Fact]
    public void GivenStringConcatObjectThreeParamsWithBoth_WhenConcat_ResultISTainted()
    {
        Assert.Equal(":+-TaintedObject-+:UntaintedObject:+-OtherTaintedObject-+:", FormatTainted(String.Concat(TaintedObject, UntaintedObject, OtherTaintedObject)));
    }

    [Fact]
    public void GivenStringConcatObjectThreeParamsWithUntainted_WhenConcat_ResultISTainted()
    {
        Assert.Equal("UntaintedObject:+-TaintedObject-+:OtherUntaintedObject", FormatTainted(String.Concat(UntaintedObject, TaintedObject, OtherUntaintedObject)));
    }

    [Fact]
    public void GivenStringConcatObjectThreeParamsWithBothJoined_WhenConcat_ResultIsTainted()
    {
        string str = String.Concat(TaintedObject, UntaintedObject, OtherTaintedObject);
        Assert.Equal(":+-TaintedObject-+:UntaintedObject:+-OtherTaintedObject-+:OtherUntaintedObject", FormatTainted(String.Concat(str, OtherUntaintedObject)));
    }

    [Fact]
    public void GivenStringConcatObjectFourParamsWithBoth_WhenConcat_ResultIsTainted()
    {
        string str = String.Concat(TaintedObject, UntaintedObject, OtherTaintedObject, OtherUntaintedObject);
        Assert.Equal(":+-TaintedObject-+:UntaintedObject:+-OtherTaintedObject-+:OtherUntaintedObject", FormatTainted(str));
    }

    [Fact]
    public void GivenStringConcatObjectFourParamsWithUntainted_WhenConcat_ResultIsTainted()
    {
        string str = String.Concat(UntaintedObject, TaintedObject, OtherUntaintedObject, OtherTaintedObject);
        Assert.Equal("UntaintedObject:+-TaintedObject-+:OtherUntaintedObject:+-OtherTaintedObject-+:", FormatTainted(str));
    }

    [Fact]
    public void GivenStringConcatObjectFourParamsWithBothJoined_WhenConcat_ResultIsTainted()
    {
        string str = String.Concat(TaintedObject, UntaintedObject, OtherTaintedObject, OtherUntaintedObject);
        Assert.Equal(":+-TaintedObject-+:UntaintedObject:+-OtherTaintedObject-+:OtherUntaintedObject:+-TaintedObject-+:", FormatTainted(String.Concat(str, TaintedObject)));
    }

    [Fact]
    public void GivenStringConcatObjectFourParamsWithBothJoinedInverse_WhenConcat_ResultIsTainted()
    {
        string str = String.Concat(TaintedObject, UntaintedObject, OtherUntaintedObject, UntaintedObject);
        Assert.Equal(":+-TaintedObject-+:UntaintedObjectOtherUntaintedObjectUntaintedObject:+-OtherTaintedObject-+:", FormatTainted(String.Concat(str, OtherTaintedObject)));
    }

    [Fact]
    public void GivenStringConcatObjectFiveParamsWithBothJoined_WhenConcat_ResultIsTainted()
    {
        string str = String.Concat(TaintedObject, UntaintedObject, OtherUntaintedObject, OtherTaintedObject, OtherTaintedObject);
        Assert.Equal(":+-TaintedObject-+:UntaintedObjectOtherUntaintedObject:+-OtherTaintedObject-+::+-OtherTaintedObject-+:OtherUntaintedObject", FormatTainted(String.Concat(str, OtherUntaintedObject)));
    }

    [Fact]
    public void GivenStringConcatObjectFiveParamsWithBothJoinedInverse_WhenConcat_ResultIsTainted()
    {
        string str = String.Concat(UntaintedObject, OtherUntaintedObject, TaintedObject, OtherTaintedObject, OtherUntaintedObject);
        Assert.Equal("UntaintedObjectOtherUntaintedObject:+-TaintedObject-+::+-OtherTaintedObject-+:OtherUntaintedObjectOtherUntaintedObject", FormatTainted(String.Concat(str, OtherUntaintedObject)));
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
        Assert.Equal("UntaintedString:+-TaintedString-+:", FormatTainted(str));
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
        string str = String.Concat<ClassForStringTest>(new List<ClassForStringTest> { new ClassForStringTest(UntaintedString), new ClassForStringTest(TaintedString) });
        Assert.Equal("UntaintedString:+-TaintedString-+:", FormatTainted(str));
    }

    [Fact]
    public void GivenATaintedStringInClassArray_WhenCallingConcat_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString:+-tainted-+:", String.Concat<ClassForStringTest>(new ClassForStringTest[] { new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue) }), () => String.Concat<ClassForStringTest>(new List<ClassForStringTest> { new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue) }));
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
        string temp = taintedValue + "W";
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

    struct StructForStringTest
    {
        readonly string str;
        public StructForStringTest(string str)
        {
            this.str = str;
        }
        public override string ToString()
        {
            return str;
        }
    }

    class ClassForStringTest
    {
        readonly string str;
        public ClassForStringTest(string str)
        {
            this.str = str;
        }
        public override string ToString()
        {
            return str;
        }
    }
}

