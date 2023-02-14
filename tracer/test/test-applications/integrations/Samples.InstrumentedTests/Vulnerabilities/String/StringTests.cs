using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Xunit;

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

    [Fact]
    public void String_Concat()
    {
        string testString1 = (string) AddTainted("01");
        string testString2 = (string) AddTainted("abc");
        string testString3 = (string) AddTainted("ABCD");
        string testString4 = (string) AddTainted(".,;:?");
        string testString5 = (string) AddTainted("+-*/{}");

        Assert.Equal(":+-01-+::+-abc-+:", Format(String.Concat(testString1, testString2)));
        Assert.Equal(":+-01-+::+-abc-+:", Format(String.Concat(testString1, null, testString2)));
        Assert.Equal(":+-01-+::+-abc-+:", Format(String.Concat((object)testString1, (object)testString2)));
        Assert.Equal(":+-01-+::+-abc-+:", Format(String.Concat((object)testString1, null, (object)testString2)));

        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+:", Format(String.Concat(testString1, testString2, testString3)));
        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+:", Format(String.Concat((object)testString1, (object)testString2, (object)testString3)));

        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+:", Format(String.Concat(testString1, testString2, testString3, testString4)));
        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+:", Format(String.Concat((object)testString1, (object)testString2, (object)testString3, (object)testString4)));

        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:", Format(String.Concat(testString1, testString2, testString3, testString4, testString5)));
        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:", Format(String.Concat((object)testString1, (object)testString2, (object)testString3, (object)testString4, (object)testString5)));

        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:", Format(String.Concat(new string[] { testString1, testString2, testString3, testString4, testString5 })));
        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:", Format(String.Concat(new object[] { testString1, testString2, testString3, testString4, testString5 })));
        Assert.Equal(":+-01-+::+-abc-+::+-ABCD-+::+-.,;:?-+::+-+-*/{}-+:", Format(String.Concat(new List<string> { testString1, testString2, testString3, testString4, testString5 })));
        Assert.Equal(":+-01-+: dummy ", Format(String.Concat(testString1, " dummy ")));
        Assert.Equal(" dummy :+-abc-+: dummy ", Format(String.Concat(" dummy ", testString2, " dummy ")));
        Assert.Equal(" dummy :+-ABCD-+:", Format(String.Concat(" dummy ", testString3)));
        Assert.Equal(":+-01-+: dummy :+-ABCD-+:", Format(String.Concat(testString1, " dummy ", testString3)));

        Assert.Equal(":+-abc-+: dummy ", Format(String.Concat(null, testString2, " dummy ")));
        Assert.Equal(":+-abc-+: dummy ", Format(String.Concat(null, testString2, (object)" dummy ")));
        Assert.Equal(" dummy :+-abc-+: dummy ", Format(String.Concat((object)" dummy ", null, testString2, (object)" dummy ")));
    }

    [Fact]
    public void String_Concat_Basic_WithTainted()
    {
        Assert.Equal("This is a tainted literal :+-TaintedString-+:", Format(String.Concat("This is a tainted literal ", TaintedString)));
    }

    [Fact]
    public void String_Concat_Basic_WithBothTainted()
    {
        Assert.Equal(":+-TaintedString-+::+-TaintedString-+:", Format(String.Concat(TaintedString, TaintedString)));
    }

    [Fact]
    public void String_Concat_Basic_WithBoth2()
    {
        Assert.Equal(":+-TaintedString-+:UntaintedString", Format(String.Concat(TaintedString, UntaintedString)));
    }

    [Fact]
    public void StringLiteralsOptimizations_Concat_0()
    {
        Assert.Equal(":+-TaintedString-+:UntaintedString", Format(String.Concat(TaintedString, "UntaintedString")));
    }

    [Fact]
    public void StringLiteralsOptimizations_Concat_1()
    {
        Assert.Equal("UntaintedString:+-TaintedString-+:", Format(String.Concat("UntaintedString", TaintedString)));
    }

    [Fact]
    public void StringLiteralsOptimizations_Concat_2Literals()
    {
        AssertNotTainted(String.Concat("Literal1", "Literal2"));
    }

    [Fact]
    public void StringLiteralsOptimizations_Concat_3Literals()
    {
        AssertNotTainted(String.Concat("Literal1", "Literal2", "Literal3"));
    }

    [Fact]
    public void StringLiteralsOptimizations_Concat_4Literals()
    {
        AssertNotTainted(String.Concat("Literal1", "Literal2", "Literal3", "Literal4"));
    }

    [Fact]
    public void StringLiteralsOptimizations_Concat_5Literals()
    {
        AssertNotTainted(String.Concat("Literal1", "Literal2", "Literal3", "Literal4", "Literal5"));
    }

    [Fact]
    public void String_Concat_ChainedLiterals()
    {
        AssertNotTainted("Literal1" + "Literal2".ToLower() + "Literal3".ToUpper() + "Literal3".Trim());
    }

    [Fact]
    public void String_Concat_Basic_WithBoth_Joined()
    {
        Assert.Equal(":+-TaintedString-+:UntaintedString:+-TaintedString-+:", Format(String.Concat(String.Concat(TaintedString, UntaintedString), TaintedString)));
    }

    [Fact]
    public void String_Concat_ThreeParams_WithBoth()
    {
        Assert.Equal(":+-TaintedString-+:UntaintedString:+-OtherTaintedString-+:", Format(String.Concat(TaintedString, UntaintedString, OtherTaintedString)));
    }

    [Fact]
    public void String_Concat_ThreeParams_WithUntainted()
    {
        Assert.Equal("UntaintedString:+-TaintedString-+:OtherUntaintedString", Format(String.Concat(UntaintedString, TaintedString, OtherUntaintedString)));
    }


    [Fact]
    public void String_Concat_ThreeParams_WithBoth_Joined()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString);
        Assert.Equal(":+-TaintedString-+:UntaintedString:+-OtherTaintedString-+:OtherUntaintedString", Format(String.Concat(str, OtherUntaintedString)));
    }

    [Fact]
    public void String_Concat_FourParams_WithBoth()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString);
        Assert.Equal(":+-TaintedString-+:UntaintedString:+-OtherTaintedString-+:OtherUntaintedString", Format(str));
    }

    [Fact]
    public void String_Concat_FourParams_WithUntainted()
    {
        string str = String.Concat(UntaintedString, TaintedString, OtherUntaintedString, OtherTaintedString);
        Assert.Equal("UntaintedString:+-TaintedString-+:OtherUntaintedString:+-OtherTaintedString-+:", Format(str));
    }

    [Fact]
    public void String_Concat_FourParams_WithBoth_Joined()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString);
        Assert.Equal(":+-TaintedString-+:UntaintedString:+-OtherTaintedString-+:OtherUntaintedString:+-TaintedString-+:", Format(String.Concat(str, TaintedString)));
    }

    [Fact]
    public void String_Concat_FourParams_WithBoth_Joined_Inverse()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherUntaintedString, UntaintedString);
        Assert.Equal(":+-TaintedString-+:UntaintedStringOtherUntaintedStringUntaintedString:+-OtherTaintedString-+:", Format(String.Concat(str, OtherTaintedString)));
    }

    [Fact]
    public void String_Concat_FiveParams_WithBoth_Joined()
    {
        string str = String.Concat(TaintedString, UntaintedString, OtherUntaintedString, OtherTaintedString, OtherTaintedString);
        Assert.Equal(":+-TaintedString-+:UntaintedStringOtherUntaintedString:+-OtherTaintedString-+::+-OtherTaintedString-+:OtherUntaintedString", Format(String.Concat(str, OtherUntaintedString)));
    }

    [Fact]
    public void String_Concat_FiveParams_WithBoth_Joined_Inverse()
    {
        string str = String.Concat(UntaintedString, OtherUntaintedString, TaintedString, OtherTaintedString, OtherUntaintedString);
        Assert.Equal("UntaintedStringOtherUntaintedString:+-TaintedString-+::+-OtherTaintedString-+:OtherUntaintedStringOtherUntaintedString", Format(String.Concat(str, OtherUntaintedString)));
    }

    [Fact]
    public void String_Concat_Object_WithTainted()
    {
        Assert.Equal("This is a tainted literal :+-TaintedObject-+:", Format(String.Concat("This is a tainted literal ", TaintedObject)));
    }

    [Fact]
    public void String_Concat_Object_WithBoth()
    {
        Assert.Equal(":+-TaintedObject-+:UntaintedObject", Format(String.Concat(TaintedObject, UntaintedObject)));
    }

    [Fact]
    public void String_Concat_Object_WithBoth_Joined()
    {
        Assert.Equal(":+-TaintedObject-+:UntaintedObject:+-TaintedObject-+:", Format(String.Concat(String.Concat(TaintedObject, UntaintedObject), TaintedObject)));
    }

    [Fact]
    public void String_Concat_Object_ThreeParams_WithBoth()
    {
        Assert.Equal(":+-TaintedObject-+:UntaintedObject:+-OtherTaintedObject-+:", Format(String.Concat(TaintedObject, UntaintedObject, OtherTaintedObject)));
    }

    [Fact]
    public void String_Concat_Object_ThreeParams_WithUntainted()
    {
        Assert.Equal("UntaintedObject:+-TaintedObject-+:OtherUntaintedObject", Format(String.Concat(UntaintedObject, TaintedObject, OtherUntaintedObject)));
    }

    [Fact]
    public void String_Concat_Object_ThreeParams_WithBoth_Joined()
    {
        string str = String.Concat(TaintedObject, UntaintedObject, OtherTaintedObject);
        Assert.Equal(":+-TaintedObject-+:UntaintedObject:+-OtherTaintedObject-+:OtherUntaintedObject", Format(String.Concat(str, OtherUntaintedObject)));
    }

    [Fact]
    public void String_Concat_Object_FourParams_WithBoth()
    {
        string str = String.Concat(TaintedObject, UntaintedObject, OtherTaintedObject, OtherUntaintedObject);
        Assert.Equal(":+-TaintedObject-+:UntaintedObject:+-OtherTaintedObject-+:OtherUntaintedObject", Format(str));
    }

    [Fact]
    public void String_Concat_Object_FourParams_WithUntainted()
    {
        string str = String.Concat(UntaintedObject, TaintedObject, OtherUntaintedObject, OtherTaintedObject);
        Assert.Equal("UntaintedObject:+-TaintedObject-+:OtherUntaintedObject:+-OtherTaintedObject-+:", Format(str));
    }

    [Fact]
    public void String_Concat_Object_FourParams_WithBoth_Joined()
    {
        string str = String.Concat(TaintedObject, UntaintedObject, OtherTaintedObject, OtherUntaintedObject);
        Assert.Equal(":+-TaintedObject-+:UntaintedObject:+-OtherTaintedObject-+:OtherUntaintedObject:+-TaintedObject-+:", Format(String.Concat(str, TaintedObject)));
    }

    [Fact]
    public void String_Concat_Object_FourParams_WithBoth_Joined_Inverse()
    {
        string str = String.Concat(TaintedObject, UntaintedObject, OtherUntaintedObject, UntaintedObject);
        Assert.Equal(":+-TaintedObject-+:UntaintedObjectOtherUntaintedObjectUntaintedObject:+-OtherTaintedObject-+:", Format(String.Concat(str, OtherTaintedObject)));
    }

    [Fact]
    public void String_Concat_Object_FiveParams_WithBoth_Joined()
    {
        string str = String.Concat(TaintedObject, UntaintedObject, OtherUntaintedObject, OtherTaintedObject, OtherTaintedObject);
        Assert.Equal(":+-TaintedObject-+:UntaintedObjectOtherUntaintedObject:+-OtherTaintedObject-+::+-OtherTaintedObject-+:OtherUntaintedObject", Format(String.Concat(str, OtherUntaintedObject)));
    }

    [Fact]
    public void String_Concat_Object_FiveParams_WithBoth_Joined_Inverse()
    {
        string str = String.Concat(UntaintedObject, OtherUntaintedObject, TaintedObject, OtherTaintedObject, OtherUntaintedObject);
        Assert.Equal("UntaintedObjectOtherUntaintedObject:+-TaintedObject-+::+-OtherTaintedObject-+:OtherUntaintedObjectOtherUntaintedObject", Format(String.Concat(str, OtherUntaintedObject)));
    }

    [Fact]
    public void String_Concat_Generic_Struct()
    {
        string str = String.Concat<S>(new List<S> { new S(UntaintedString), new S(TaintedString) });
        Assert.Equal("UntaintedString:+-TaintedString-+:", Format(str));
    }

    [Fact]
    public void String_Concat_Generic_Class()
    {
        string str = String.Concat<C>(new List<C> { new C(UntaintedString), new C(TaintedString) });
        Assert.Equal("UntaintedString:+-TaintedString-+:", Format(str));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWith2StringParams_ResultIsTainted()
    {
        DES.Create();
        AssertTaintedWithOriginalCallCheck("concat:+-tainted-+:", () => String.Concat("concat", taintedValue), () => String.Concat("concat", taintedValue));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWith3StringParams_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-TAINTED2-+:concat:+-tainted-+:", () => String.Concat(taintedValue2, "concat", taintedValue), () => String.Concat(taintedValue2, "concat", taintedValue));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWith4Params_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-TAINTED2-+:concat:+-tainted-+:CONCAT2", () => String.Concat(taintedValue2, "concat", taintedValue, "CONCAT2"), () => String.Concat(taintedValue2, "concat", taintedValue, "CONCAT2"));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingConcatWithStringArrayParam_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("concatCONCAT2:+-tainted-+::+-TAINTED2-+:", () => String.Concat(new string[] { "concat", "CONCAT2", taintedValue, taintedValue2 }), () => String.Concat(new string[] { "concat", "CONCAT2", taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWithObjectArrayParam_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("concatconcat2:+-tainted-+::+-TAINTED2-+:", () => String.Concat(new object[] { "concat", "concat2", taintedValue, taintedValue2 }), () => String.Concat(new object[] { "concat", "concat2", taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingConcatWith2ObjectParams_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("concat:+-tainted-+:", () => String.Concat((object)"concat", taintedValue), () => String.Concat((object)"concat", taintedValue));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingConcatWith2ObjectParams_ResultIsTainted2()
    {
        AssertTaintedWithOriginalCallCheck("concat:+-tainted-+:", () => String.Concat("concat", (object)taintedValue), () => String.Concat("concat", (object)taintedValue));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingConcatWith3ObjectParams_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-TAINTED2-+:concat:+-tainted-+:", () => String.Concat(taintedValue2, (object)"concat", taintedValue), () => String.Concat(taintedValue2, (object)"concat", taintedValue));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingConcatWith4ObjectParams_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-TAINTED2-+:concat:+-tainted-+:concat2", () => String.Concat(taintedValue2, (object)"concat", taintedValue, (object)"concat2"), () => String.Concat(taintedValue2, (object)"concat", taintedValue, (object)"concat2"));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingConcatWithStringIEnumerableStringParam_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("concatCONCAT2:+-tainted-+::+-TAINTED2-+:", () => String.Concat(new List<string> { "concat", "CONCAT2", taintedValue, taintedValue2 }), () => String.Concat(new List<string> { "concat", "CONCAT2", taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingConcatWithObjectListParam_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("concatconcat2:+-tainted-+::+-TAINTED2-+:", () => String.Concat(new List<object> { "concat", "concat2", taintedValue, taintedValue2 }), () => String.Concat(new List<object> { "concat", "concat2", taintedValue, taintedValue2 }));
    }

    [Fact]
    
    public void GivenATaintedString_WhenCallingConcatWithStringIEnumerableNullParam_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("concatCONCAT2:+-TAINTED2-+:", () => String.Concat(new List<string> { "concat", "CONCAT2", null, taintedValue2 }), () => String.Concat(new List<string> { "concat", "CONCAT2", null, taintedValue2 }));
    }

    [Fact]
    
    public void GivenATaintedObject_WhenCallingConcatWithGenericObjectArrayParam_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("concatconcat2:+-tainted-+::+-TAINTED2-+:", () => String.Concat<object>(new object[] { "concat", "concat2", taintedValue, taintedValue2 }), () => String.Concat<object>(new object[] { "concat", "concat2", taintedValue, taintedValue2 }));
    }

    [Fact]
    
    public void GivenATaintedObject_WhenCallingConcatWithGenericObjectListParam_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("concatconcat2:+-tainted-+::+-TAINTED2-+:", () => String.Concat<object>(new List<object> { "concat", "concat2", taintedValue, taintedValue2 }), () => String.Concat<object>(new List<object> { "concat", "concat2", taintedValue, taintedValue2 }));
    }

    [Fact]
    
    public void GivenATaintedString_WhenCallingConcatWithStringIEnumerableStringParam_ResultIsTainted2()
    {
        AssertTaintedWithOriginalCallCheck("concatCONCAT2:+-tainted-+::+-TAINTED2-+:", () => String.Concat<string>(new List<string> { "concat", "CONCAT2", taintedValue, taintedValue2 }), () => String.Concat<string>(new List<string> { "concat", "CONCAT2", taintedValue, taintedValue2 }));
    }

    [Fact]
    
    public void GivenATaintedString_WhenCallingConcatWithStringArrayNullParam_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("concatCONCAT2:+-TAINTED2-+:", () => String.Concat(new string[] { "concat", "CONCAT2", null, taintedValue2 }), () => String.Concat(new string[] { "concat", "CONCAT2", null, taintedValue2 }));
    }

    [Fact]
    
    public void GivenATaintedString_WhenCallingConcatWithStringNullParam_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("concatCONCAT2:+-TAINTED2-+:", () => String.Concat("concat", "CONCAT2", null, taintedValue2), () => String.Concat("concat", "CONCAT2", null, taintedValue2));
    }

    
    [Fact]
    public void GivenATaintedStringInStrcut_WhenCallingConcat_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("UntaintedString:+-tainted-+:", () => String.Concat<StructForTest>(new List<StructForTest> { new StructForTest("UntaintedString"), new StructForTest(taintedValue) }), () => String.Concat<StructForTest>(new List<StructForTest> { new StructForTest("UntaintedString"), new StructForTest(taintedValue) }));
    }

    
    [Fact]
    public void GivenATaintedStringInStrcut_WhenCallingConcat_ResultIsTainted2()
    {
        AssertTaintedWithOriginalCallCheck("UntaintedString:+-tainted-+:", () => String.Concat<StructForTest2>(new List<StructForTest2> { new StructForTest2("UntaintedString"), new StructForTest2(taintedValue) }), () => String.Concat<StructForTest2>(new List<StructForTest2> { new StructForTest2("UntaintedString"), new StructForTest2(taintedValue) }));
    }

struct S
    {
        readonly string str;
        public S(string str)
        {
            this.str = str;
        }
        public override string ToString()
        {
            return str;
        }
    }

    class C
    {
        readonly string str;
        public C(string str)
        {
            this.str = str;
        }
        public override string ToString()
        {
            return str;
        }
    }

    struct StructForTest
    {
        readonly string str;
        public StructForTest(string str)
        {
            this.str = str;
        }
        public override string ToString()
        {
            return str;
        }
    }

    class StructForTest2
    {
        readonly string str;
        public StructForTest2(string str)
        {
            this.str = str;
        }
        public override string ToString()
        {
            return str;
        }
    }

}

