using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Propagation.String;

public class StringJoinTests : InstrumentationTestsBase
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

    public StringJoinTests()
    {
        AddTainted(taintedValue);
        AddTainted(taintedValue2);
        AddTainted(TaintedObject);
        AddTainted(OtherTaintedObject);
        AddTainted(TaintedString);
        AddTainted(OtherTaintedString);
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithObjectArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,:+-TAINTED2-+:", System.String.Join(",", new object[] { taintedValue, taintedValue2 }), () => System.String.Join(",", new object[] { taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithObjectArray_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,:+-TAINTED2-+:", System.String.Join(",", taintedValue, taintedValue2), () => System.String.Join(",", taintedValue , taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithObjectArrayAndTaintedSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+::+-tainted-+:eee", System.String.Join(taintedValue, new object[] { taintedValue2, "eee" }), () => System.String.Join(taintedValue, new object[] { taintedValue2, "eee" }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,:+-TAINTED2-+:", System.String.Join(",", new string[] { taintedValue, taintedValue2 }), () => System.String.Join(",", new string[] { taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArray_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,:+-TAINTED2-+:", System.String.Join(",", taintedValue, taintedValue2), () => System.String.Join(",", taintedValue, taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndTaintedSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+::+-tainted-+:eee", System.String.Join(taintedValue, new string[] { taintedValue2, "eee" }), () => System.String.Join(taintedValue, new string[] { taintedValue2, "eee" }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringList_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,:+-TAINTED2-+:", System.String.Join(",", new List<string> { taintedValue, taintedValue2 }), () => System.String.Join(",", new List<string> { taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringListAndTaintedSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+::+-tainted-+:eee", System.String.Join(taintedValue, new List<string> { taintedValue2, "eee" }), () => System.String.Join(taintedValue, new List<string> { taintedValue2, "eee" }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringListAndTaintedSeparator_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+:", System.String.Join(taintedValue, new List<string> { taintedValue2}), () => System.String.Join(taintedValue, new List<string> { taintedValue2}));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndIndex_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", System.String.Join(",", new string[] { taintedValue, taintedValue2 }, 0, 1), () => System.String.Join(",", new string[] { taintedValue, taintedValue2 }, 0, 1));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndIndex_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,:+-TAINTED2-+:", System.String.Join(",", new string[] { taintedValue, taintedValue2 }, 0, 2), () => System.String.Join(",", new string[] { taintedValue, taintedValue2 }, 0, 2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndIndex_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,:+-TAINTED2-+:", System.String.Join(",", new string[] { taintedValue, taintedValue2 }), () => System.String.Join(",", new string[] { taintedValue, taintedValue2 }, 0, 2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndIndex_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", System.String.Join(",", new string[] { taintedValue }), () => System.String.Join(",", new string[] { taintedValue}, 0, 1));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndIndex_ResultIsTainted5()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", System.String.Join(",", new string[] { taintedValue }), () => System.String.Join(",", new string[] { taintedValue }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndIndexAndTaintedSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+::+-tainted-+:eee", System.String.Join(taintedValue, new string[] { taintedValue2, "eee" }, 0, 2), () => System.String.Join(taintedValue, new string[] { taintedValue2, "eee" }, 0, 2));
    }

#if NETFRAMEWORK
    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithObjectArrayAndTaintedSeparatorOneNullParams_ResultIsTainted()
    {

        AssertUntaintedWithOriginalCallCheck(System.String.Empty, System.String.Join(taintedValue, new object[] { null, "eee" }),
                () => System.String.Join(taintedValue, new object[] { null, "eee" }));
    }
#else
    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithObjectArrayAndTaintedSeparatorOneNullParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:eee", System.String.Join(taintedValue, new object[] { null, "eee" }), 
            () => System.String.Join(taintedValue, new object[] { null, "eee" }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithObjectArrayAndTaintedSeparatorOneNullParams_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+:", System.String.Join(taintedValue, new object[] { taintedValue2 }), 
            () => System.String.Join(taintedValue, new object[] { taintedValue2 }));
    }
#endif
    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayOneNullParams_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(",:+-TAINTED2-+:", System.String.Join(",", null, taintedValue2), () => System.String.Join(",", null, taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndIndexOneNullParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(",:+-TAINTED2-+:", System.String.Join(",", new string[] { null, taintedValue2 }, 0, 2), () => System.String.Join(",", new string[] { null, taintedValue2 }, 0, 2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithObjectArrayAndNullSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+:eee", System.String.Join(null, new object[] { taintedValue2, "eee" }), () => System.String.Join(null, new object[] { taintedValue2, "eee" }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayNullSeparator_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-TAINTED2-+:", System.String.Join(null, taintedValue, taintedValue2), () => System.String.Join(null, taintedValue, taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringListAndNullSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+:eee", System.String.Join(null, new List<string> { taintedValue2, "eee" }), () => System.String.Join(null, new List<string> { taintedValue2, "eee" }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndIndexAndNullSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+:eee", System.String.Join(null, new string[] { taintedValue2, "eee" }, 0, 2), () => System.String.Join(null, new string[] { taintedValue2, "eee" }, 0, 2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithGenericListNullSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+:eee", System.String.Join<string>(null, new List<string> { taintedValue2, "eee" }), () => System.String.Join<string>(null, new List<string> { taintedValue2, "eee" }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithGenericListOneNullParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+::+-tainted-+:", System.String.Join<string>(taintedValue, new List<string> { taintedValue2, null }), () => System.String.Join<string>(taintedValue, new List<string> { taintedValue2, null }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithGenericList_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+::+-tainted-+:eee", System.String.Join<string>(taintedValue, new List<string> { taintedValue2, "eee" }), () => System.String.Join<string>(taintedValue, new List<string> { taintedValue2, "eee" }));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", System.String.Join<StructForStringTest>(",", new List<StructForStringTest> { new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue) }), () => System.String.Join<StructForStringTest>(",", new List<StructForStringTest> { new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue) }));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", System.String.Join(",", new List<StructForStringTest> { new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue) }), () => System.String.Join(",", new List<StructForStringTest> { new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue) }));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", System.String.Join(",", new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue)), () => System.String.Join(",", new object[] { new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue) }));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", System.String.Join(",", new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue)), () => System.String.Join(",", new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue)));
    }

    [Fact]
    public void GivenATaintedStringInClass_WhenCallingJoin_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", System.String.Join<ClassForStringTest>(",", new List<ClassForStringTest> { new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue) }), () => System.String.Join<ClassForStringTest>(",", new List<ClassForStringTest> { new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue) }));
    }

    [Fact]
    public void GivenATaintedStringInClass_WhenCallingJoin_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", System.String.Join(",", new List<ClassForStringTest> { new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue) }), () => System.String.Join(",", new List<ClassForStringTest> { new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue) }));
    }

    [Fact]
    public void GivenATaintedStringInClass_WhenCallingJoin_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", System.String.Join(",", new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue)), () => System.String.Join(",", new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue)));
    }

    [Fact]
    public void GivenATaintedStringInClass_WhenCallingJoin_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", System.String.Join(",", new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue)), () => System.String.Join(",", new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue)));
    }

    [Fact]
    public void GivenATaintedStringInClass_WhenCallingJoin_ResultIsTainted5()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", System.String.Join(",", new ClassForStringTest(taintedValue)), () => System.String.Join(",", new ClassForStringTest(taintedValue)));
    }

#if NETCOREAPP3_1_OR_GREATER

    [Fact]
    public void GivenATaintedStringInList_WhenCallingJoinWithChar_ResultIsTainted10()
    {
        var objectList = new List<object> { TaintedObject, UntaintedObject, OtherTaintedObject };
        AssertTaintedFormatWithOriginalCallCheck(
            ":+-TaintedObject-+: UntaintedObject :+-OtherTaintedObject-+:",
            string.Join(' ', objectList),
            () => string.Join(' ', objectList));
    }

    [Fact]
    public void GivenATaintedStringInNestedMethodObject_WhenCallingJoinWithChar_ResultIsTainted6()
    {
        void NestedMethod<T>(List<T> parameters)
        {
            AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", System.String.Join(',', parameters), () => System.String.Join(',', parameters));
        }

        NestedMethod(new List<string> { taintedValue });
    }

    [Fact]
    public void GivenATaintedStringInNestedMethodObject_WhenCallingJoinWithChar_ResultIsTainted7()
    {
        void NestedMethod<T>(List<T> parameters)
        {
            AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,NonTainted", System.String.Join(',', parameters), () => System.String.Join(',', parameters));
        }

        NestedMethod(new List<string> { taintedValue, "NonTainted" });
    }

    [Fact]
    public void GivenATaintedStringInList_WhenCallingJoinWithChar_ResultIsTainted8()
    {
        var parameters = new List<string> { taintedValue, "NonTainted" };
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,NonTainted", System.String.Join(',', parameters), () => System.String.Join(',', parameters));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingJoin_ResultIsTainted7()
    {
        System.String.Concat(taintedValue, "eee");
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:",
            System.String.Join('a', taintedValue, taintedValue),
            () => System.String.Join('a', taintedValue, taintedValue));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingJoin_ResultIsTainted8()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:",
            System.String.Join('a', (object)taintedValue, (object)taintedValue),
            () => System.String.Join('a', (object)taintedValue, (object)taintedValue));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingJoin_ResultIsTainted9()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:a1",
            System.String.Join('a', (object)taintedValue, (object)taintedValue, 1),
            () => System.String.Join('a', (object)taintedValue, (object)taintedValue, 1));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingJoin_ResultIsTainted10()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:",
            System.String.Join('a', new string[] { taintedValue, taintedValue }, 0, 2),
            () => System.String.Join('a', new string[] { taintedValue, taintedValue }, 0, 2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingJoin_ResultIsTainted12()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:",
            System.String.Join('a', new List<string> { taintedValue, taintedValue }),
            () => System.String.Join('a', new List<string> { taintedValue, taintedValue }));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingJoin_ResultIsTainted13()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:aa:+-tainted-+:",
            System.String.Join('a', new List<string> { taintedValue, null, taintedValue }),
            () => System.String.Join('a', new List<string> { taintedValue, null, taintedValue }));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingJoin_ResultIsTainted14()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:",
            System.String.Join('a', new List<string> { taintedValue, taintedValue }),
            () => System.String.Join('a', new List<string> { taintedValue, taintedValue }));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingJoin_ResultIsTainted15()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:aa:+-tainted-+:",
            System.String.Join<string>('a', new List<string> { taintedValue, null, taintedValue }),
            () => System.String.Join<string>('a', new List<string> { taintedValue, null, taintedValue }));
    }
#endif

    [Fact]
    public void GivenATaintedString_WhenCallingJoin_ResultIsTainted16()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:",
            System.String.Join("a", new string[] { taintedValue, taintedValue }, 0, 2),
            () => System.String.Join("a", new string[] { taintedValue, taintedValue }, 0, 2));
    }

    [Fact]
    public void GivenSomeStrings_WhenJoin_ResultIsOk()
    {
        string separator = "-";
        string testString1 = (string) AddTainted("01");
        string testString2 = (string) AddTainted("abc");

        AssertTaintedFormatWithOriginalCallCheck(":+-01-+:", System.String.Join("-", testString1), () => System.String.Join("-",testString1));
        AssertTaintedFormatWithOriginalCallCheck(":+-01-+:", System.String.Join(separator, testString1), () => System.String.Join(separator, testString1));
        AssertTaintedFormatWithOriginalCallCheck(":+-01-+:-:+-abc-+:", System.String.Join("-", testString1, testString2), () => System.String.Join("-", testString1, testString2));
    }

    [Fact]
    public void GivenStringJoinBasicWithBoth_WhenJoin_ResultIsOk()
    {
        string[] sArr = new string[] { TaintedString, UntaintedString };
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:|UntaintedString", System.String.Join("|", sArr), () => System.String.Join("|", sArr));
    }

    [Fact]
    public void GivenStringJoinBasicWithTwoTainted_WhenJoin_ResultIsOk()
    {
        string[] sArr = new string[] { TaintedString, UntaintedString, OtherTaintedString };
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:|UntaintedString|:+-OtherTaintedString-+:", System.String.Join("|", sArr), () => System.String.Join("|", sArr));
    }

    [Fact]
    public void GivenStringJoinBasicWithTwoUntainted_WhenJoin_ResultIsOk()
    {
        string[] sArr = new string[] { UntaintedString, TaintedString, OtherUntaintedString };
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString|:+-TaintedString-+:|OtherUntaintedString", System.String.Join("|", sArr), () => System.String.Join("|", sArr));
    }

    [Fact]
    public void GivenStringJoinBasicWithTwoTaintedAndTwoUntainted_WhenJoin_ResultIsOk()
    {
        string[] sArr = new string[] { TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString };
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:|UntaintedString|:+-OtherTaintedString-+:|OtherUntaintedString", 
            System.String.Join("|", sArr), 
            () => System.String.Join("|", sArr));
    }

    [Fact]
    public void GivenStringJoinObjectWithBoth_WhenJoin_ResultIsOk()
    {
        object[] sArr = new object[] { TaintedObject, UntaintedObject };
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedObject-+:|UntaintedObject", System.String.Join("|", sArr), () => System.String.Join("|", sArr));
    }

    [Fact]
    public void GivenStringJoinObjectWithTwoTainted_WhenJoin_ResultIsOk()
    {
        object[] sArr = new object[] { TaintedObject, UntaintedObject, OtherTaintedObject };
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedObject-+:|UntaintedObject|:+-OtherTaintedObject-+:", System.String.Join("|", sArr), () => System.String.Join("|", sArr));
    }

    [Fact]
    public void GivenStringJoinObjectWithTwoUntainted_WhenJoin_ResultIsOk()
    {
        object[] sArr = new object[] { UntaintedObject, TaintedObject, OtherUntaintedObject };
        AssertTaintedFormatWithOriginalCallCheck("UntaintedObject|:+-TaintedObject-+:|OtherUntaintedObject", System.String.Join("|", sArr), () => System.String.Join("|", sArr));
    }

    [Fact]
    public void GivenStringJoinObjectWithTwoTaintedAndTwoUntainted_WhenJoin_ResultIsOk()
    {
        object[] sArr = new object[] { TaintedObject, UntaintedObject, OtherTaintedObject, OtherUntaintedObject };
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedObject-+:|UntaintedObject|:+-OtherTaintedObject-+:|OtherUntaintedObject", 
            System.String.Join("|", sArr),
            () => System.String.Join("|", sArr));
    }

    [Fact]
    public void GivenStringJoinIndexWithBoth_WhenJoin_ResultIsOk()
    {
        string[] sArr = new string[] { TaintedString, UntaintedString, OtherUntaintedString };
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:|UntaintedString", 
            System.String.Join("|", sArr, 0, 2),
            () => System.String.Join("|", sArr, 0, 2));
    }

    [Fact]
    public void GivenStringJoinIndexWithTwoTainted_WhenJoin_ResultIsOk()
    {
        string[] sArr = new string[] { TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString };
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:|UntaintedString|:+-OtherTaintedString-+:",
            System.String.Join("|", sArr, 0, 3),
            () => System.String.Join("|", sArr, 0, 3));
    }

    [Fact]
    public void GivenStringJoinIndexWithTwoUntainted_WhenJoin_ResultIsOk()
    {
        string[] sArr = new string[] { UntaintedString, TaintedString, OtherUntaintedString, OtherUntaintedString };
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString|:+-TaintedString-+:|OtherUntaintedString",
            System.String.Join("|", sArr, 0, 3),
            () => System.String.Join("|", sArr, 0, 3));
    }

    [Fact]
    public void GivenStringJoinIndexWithTwoTaintedAndTwoUntainted_WhenJoin_ResultIsOk()
    {
        string[] sArr = new string[] { TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString, OtherUntaintedString };
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:|UntaintedString|:+-OtherTaintedString-+:|OtherUntaintedString",
            System.String.Join("|", sArr, 0, 4),
            () => System.String.Join("|", sArr, 0, 4));
    }

    [Fact]
    public void GivenStringJoinIndexWithTwoTaintedAndTwoUntaintedChunk_WhenJoin_ResultIsOk()
    {
        string[] sArr = new string[] { TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString, OtherUntaintedString };
        AssertTaintedFormatWithOriginalCallCheck(":+-OtherTaintedString-+:|OtherUntaintedString", System.String.Join("|", sArr, 2, 2), () => System.String.Join("|", sArr, 2, 2));
    }

    [Fact]
    public void GivenStringJoinListWithBoth_WhenJoin_ResultIsOk()
    {
        List<string> list = new List<string>() { TaintedString, UntaintedString };
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:|UntaintedString", System.String.Join("|", list), () => System.String.Join("|", list));
    }

    [Fact]
    public void GivenStringJoinListWithTwoTainted_WhenJoin_ResultIsOk()
    {
        List<string> list = new List<string>() { TaintedString, UntaintedString, OtherTaintedString };
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:|UntaintedString|:+-OtherTaintedString-+:", System.String.Join("|", list), () => System.String.Join("|", list));
    }

    [Fact]
    public void GivenStringJoinListWithTwoUntainted_WhenJoin_ResultIsOk()
    {
        List<string> list = new List<string>() { UntaintedString, TaintedString, OtherUntaintedString };
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString|:+-TaintedString-+:|OtherUntaintedString", System.String.Join("|", list), () => System.String.Join("|", list));
    }

    [Fact]
    public void GivenStringJoinListWithTwoTaintedAndTwoUntainted_WhenJoin_ResultIsOk()
    {
        List<string> list = new List<string>() { TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString };
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:|UntaintedString|:+-OtherTaintedString-+:|OtherUntaintedString", 
            System.String.Join("|", list), 
            () => System.String.Join("|", list));
    }

    [Fact]
    public void GivenStringJoinTGenericStruct_WhenJoin_ResultIsOk()
    {
        var list = new List<StructForStringTest> { new StructForStringTest(UntaintedString), new StructForStringTest(TaintedString) };
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString|:+-TaintedString-+:", 
            string.Join<StructForStringTest>("|", list),
            () => string.Join<StructForStringTest>("|", list));
    }

    [Fact]
    public void GivenStringJoinTGenericClass_WhenJoin_ResultIsOk()
    {
        var list = new List<ClassForStringTest> { new ClassForStringTest(UntaintedString), new ClassForStringTest(TaintedString) };
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString|:+-TaintedString-+:",
            string.Join<ClassForStringTest>("|", list),
            () => string.Join<ClassForStringTest>("|", list));
    }

#if NET9_0_OR_GREATER

    [Fact]
    public void GivenStringAndStringReadOnlySpan_WhenJoin_ResultIsOk()
    {
        var values = new string[] { TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString };
        var span = new ReadOnlySpan<string>(values);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:|UntaintedString|:+-OtherTaintedString-+:|OtherUntaintedString",
            System.String.Join("|", span),
            () => System.String.Join("|", values)); // Use values here as we can not use span in the lambda expression
    }

    [Fact]
    public void GivenATaintedStringInAStringReadOnlySpan_WhenCallingJoin_ResultIsTainted()
    {
        var values = new string[] { taintedValue, taintedValue};
        var span = new ReadOnlySpan<string>(values);
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:",
            System.String.Join('a', span),
            () => System.String.Join('a', values)); // Use values here as we can not use span in the lambda expression
    }

    [Fact]
    public void GivenStringAndObjectReadOnlySpan_WhenJoin_ResultIsOk()
    {
        var values = new object[] { TaintedString, UntaintedString, OtherTaintedString, 1 };
        var span = new ReadOnlySpan<object>(values);
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:|UntaintedString|:+-OtherTaintedString-+:|1",
            System.String.Join("|", span),
            () => System.String.Join("|", values)); // Use values here as we can not use span in the lambda expression
    }

    [Fact]
    public void GivenATaintedStringInAnObjectReadOnlySpan_WhenCallingJoin_ResultIsTainted()
    {
        var values = new object[] { taintedValue, taintedValue, 1 };
        var span = new ReadOnlySpan<object>(values);
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:a1",
            System.String.Join('a', span),
            () => System.String.Join('a', values)); // Use values here as we can not use span in the lambda expression
    }

#endif
}
