using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;

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
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,:+-TAINTED2-+:", String.Join(",", new object[] { taintedValue, taintedValue2 }), () => String.Join(",", new object[] { taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithObjectArray_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,:+-TAINTED2-+:", String.Join(",", taintedValue, taintedValue2), () => String.Join(",", taintedValue, taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithObjectArrayAndTaintedSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+::+-tainted-+:eee", String.Join(taintedValue, new object[] { taintedValue2, "eee" }), () => String.Join(taintedValue, new object[] { taintedValue2, "eee" }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,:+-TAINTED2-+:", String.Join(",", new string[] { taintedValue, taintedValue2 }), () => String.Join(",", new string[] { taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArray_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,:+-TAINTED2-+:", String.Join(",", taintedValue, taintedValue2), () => String.Join(",", taintedValue, taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndTaintedSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+::+-tainted-+:eee", String.Join(taintedValue, new string[] { taintedValue2, "eee" }), () => String.Join(taintedValue, new string[] { taintedValue2, "eee" }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringList_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,:+-TAINTED2-+:", String.Join(",", new List<string> { taintedValue, taintedValue2 }), () => String.Join(",", new List<string> { taintedValue, taintedValue2 }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringListAndTaintedSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+::+-tainted-+:eee", String.Join(taintedValue, new List<string> { taintedValue2, "eee" }), () => String.Join(taintedValue, new List<string> { taintedValue2, "eee" }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndIndex_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", String.Join(",", new string[] { taintedValue, taintedValue2 }, 0, 1), () => String.Join(",", new string[] { taintedValue, taintedValue2 }, 0, 1));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndIndex_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,:+-TAINTED2-+:", String.Join(",", new string[] { taintedValue, taintedValue2 }, 0, 2), () => String.Join(",", new string[] { taintedValue, taintedValue2 }, 0, 2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndIndex_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:,:+-TAINTED2-+:", String.Join(",", new string[] { taintedValue, taintedValue2 }), () => String.Join(",", new string[] { taintedValue, taintedValue2 }, 0, 2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndIndexAndTaintedSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+::+-tainted-+:eee", String.Join(taintedValue, new string[] { taintedValue2, "eee" }, 0, 2), () => String.Join(taintedValue, new string[] { taintedValue2, "eee" }, 0, 2));
    }

#if !NETCORE31 && !NETCORE21 && !NET50 && !NET60
    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithObjectArrayAndTaintedSeparatorOneNullParams_ResultIsTainted()
    {
        Assert.Equal(String.Empty, String.Join(taintedValue, new object[] { null, "eee" }));
    }
#else
    [Fact]
        public void GivenATaintedObject_WhenCallingJoinWithObjectArrayAndTaintedSeparatorOneNullParams_ResultIsTainted()
        {
            AssertTaintedFormatWithOriginalCallCheck("taintedeee", String.Join(taintedValue, new object[] { null, "eee" }), 
                () => String.Join(taintedValue, new object[] { null, "eee" }));
        }
#endif
    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayOneNullParams_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(",:+-TAINTED2-+:", String.Join(",", null, taintedValue2), () => String.Join(",", null, taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndIndexOneNullParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(",:+-TAINTED2-+:", String.Join(",", new string[] { null, taintedValue2 }, 0, 2), () => String.Join(",", new string[] { null, taintedValue2 }, 0, 2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithObjectArrayAndNullSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+:eee", String.Join(null, new object[] { taintedValue2, "eee" }), () => String.Join(null, new object[] { taintedValue2, "eee" }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayNullSeparator_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-TAINTED2-+:", String.Join(null, taintedValue, taintedValue2), () => String.Join(null, taintedValue, taintedValue2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringListAndNullSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+:eee", String.Join(null, new List<string> { taintedValue2, "eee" }), () => String.Join(null, new List<string> { taintedValue2, "eee" }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithStringArrayAndIndexAndNullSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+:eee", String.Join(null, new string[] { taintedValue2, "eee" }, 0, 2), () => String.Join(null, new string[] { taintedValue2, "eee" }, 0, 2));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithGenericListNullSeparator_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+:eee", String.Join<string>(null, new List<string> { taintedValue2, "eee" }), () => String.Join<string>(null, new List<string> { taintedValue2, "eee" }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithGenericListOneNullParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+::+-tainted-+:", String.Join<string>(taintedValue, new List<string> { taintedValue2, null }), () => String.Join<string>(taintedValue, new List<string> { taintedValue2, null }));
    }

    [Fact]
    public void GivenATaintedObject_WhenCallingJoinWithGenericList_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TAINTED2-+::+-tainted-+:eee", String.Join<string>(taintedValue, new List<string> { taintedValue2, "eee" }), () => String.Join<string>(taintedValue, new List<string> { taintedValue2, "eee" }));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", String.Join<StructForStringTest>(",", new List<StructForStringTest> { new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue) }), () => String.Join<StructForStringTest>(",", new List<StructForStringTest> { new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue) }));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", String.Join(",", new List<StructForStringTest> { new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue) }), () => String.Join(",", new List<StructForStringTest> { new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue) }));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", String.Join(",", new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue)), () => String.Join(",", new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue)));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", String.Join(",", new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue)), () => String.Join(",", new StructForStringTest("UntaintedString"), new StructForStringTest(taintedValue)));
    }

    [Fact]
    public void GivenATaintedStringInClass_WhenCallingJoin_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", String.Join<ClassForStringTest>(",", new List<ClassForStringTest> { new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue) }), () => String.Join<ClassForStringTest>(",", new List<ClassForStringTest> { new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue) }));
    }

    [Fact]
    public void GivenATaintedStringInClass_WhenCallingJoin_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", String.Join(",", new List<ClassForStringTest> { new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue) }), () => String.Join(",", new List<ClassForStringTest> { new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue) }));
    }

    [Fact]
    public void GivenATaintedStringInClass_WhenCallingJoin_ResultIsTainted4()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", String.Join(",", new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue)), () => String.Join(",", new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue)));
    }

    [Fact]
    public void GivenATaintedStringInClass_WhenCallingJoin_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck("UntaintedString,:+-tainted-+:", String.Join(",", new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue)), () => String.Join(",", new ClassForStringTest("UntaintedString"), new ClassForStringTest(taintedValue)));
    }

#if !NET462
    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted7()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:",
            String.Join('a', taintedValue, taintedValue),
            () => String.Join('a', taintedValue, taintedValue));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted8()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:",
            String.Join('a', (object)taintedValue, (object)taintedValue),
            () => String.Join('a', (object)taintedValue, (object)taintedValue));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted9()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:a1",
            String.Join('a', (object)taintedValue, (object)taintedValue, 1),
            () => String.Join('a', (object)taintedValue, (object)taintedValue, 1));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted10()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:",
            String.Join('a', new string[] { taintedValue, taintedValue }, 0, 2),
            () => String.Join('a', new string[] { taintedValue, taintedValue }, 0, 2));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted12()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:",
            String.Join('a', new List<string> { taintedValue, taintedValue }),
            () => String.Join('a', new List<string> { taintedValue, taintedValue }));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted13()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:aa:+-tainted-+:",
            String.Join('a', new List<string> { taintedValue, null, taintedValue }),
            () => String.Join('a', new List<string> { taintedValue, null, taintedValue }));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted14()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:",
            String.Join('a', new List<string> { taintedValue, taintedValue }),
            () => String.Join('a', new List<string> { taintedValue, taintedValue }));
    }

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted15()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:aa:+-tainted-+:",
            String.Join<string>('a', new List<string> { taintedValue, null, taintedValue }),
            () => String.Join<string>('a', new List<string> { taintedValue, null, taintedValue }));
    }
#endif

    [Fact]
    public void GivenATaintedStringInStruct_WhenCallingJoin_ResultIsTainted16()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:a:+-tainted-+:",
            String.Join("a", new string[] { taintedValue, taintedValue }, 0, 2),
            () => String.Join("a", new string[] { taintedValue, taintedValue }, 0, 2));
    }

    [Fact]
    public void String_Join()
    {
        string separator = "-";
        string testString1 = (string) AddTainted("01");
        string testString2 = (string) AddTainted("abc");

        Assert.Equal(":+-01-+:", FormatTainted(String.Join("-", testString1)));
        Assert.Equal(":+-01-+:", FormatTainted(String.Join(separator, testString1)));
        Assert.Equal(":+-01-+:-:+-abc-+:", FormatTainted(String.Join("-", testString1, testString2)));
    }

    [Fact]
    public void String_Join_Basic_With_Both()
    {
        string[] sArr = new string[] { TaintedString, UntaintedString };
        Assert.Equal(":+-TaintedString-+:|UntaintedString", FormatTainted(String.Join("|", sArr)));
    }

    [Fact]
    public void String_Join_Basic_With_Two_Tainted()
    {
        string[] sArr = new string[] { TaintedString, UntaintedString, OtherTaintedString };
        Assert.Equal(":+-TaintedString-+:|UntaintedString|:+-OtherTaintedString-+:", FormatTainted(String.Join("|", sArr)));
    }

    [Fact]
    public void String_Join_Basic_With_Two_Untainted()
    {
        string[] sArr = new string[] { UntaintedString, TaintedString, OtherUntaintedString };
        Assert.Equal("UntaintedString|:+-TaintedString-+:|OtherUntaintedString", FormatTainted(String.Join("|", sArr)));
    }

    [Fact]
    public void String_Join_Basic_With_Two_Tainted_And_Two_Untainted()
    {
        string[] sArr = new string[] { TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString };
        Assert.Equal(":+-TaintedString-+:|UntaintedString|:+-OtherTaintedString-+:|OtherUntaintedString", FormatTainted(String.Join("|", sArr)));
    }

    [Fact]
    public void String_Join_Object_With_Both()
    {
        object[] sArr = new object[] { TaintedObject, UntaintedObject };
        Assert.Equal(":+-TaintedObject-+:|UntaintedObject", FormatTainted(String.Join("|", sArr)));
    }

    [Fact]
    public void String_Join_Object_With_Two_Tainted()
    {
        object[] sArr = new object[] { TaintedObject, UntaintedObject, OtherTaintedObject };
        Assert.Equal(":+-TaintedObject-+:|UntaintedObject|:+-OtherTaintedObject-+:", FormatTainted(String.Join("|", sArr)));
    }

    [Fact]
    public void String_Join_Object_With_Two_Untainted()
    {
        object[] sArr = new object[] { UntaintedObject, TaintedObject, OtherUntaintedObject };
        Assert.Equal("UntaintedObject|:+-TaintedObject-+:|OtherUntaintedObject", FormatTainted(String.Join("|", sArr)));
    }

    [Fact]
    public void String_Join_Object_With_Two_Tainted_And_Two_Untainted()
    {
        object[] sArr = new object[] { TaintedObject, UntaintedObject, OtherTaintedObject, OtherUntaintedObject };
        Assert.Equal(":+-TaintedObject-+:|UntaintedObject|:+-OtherTaintedObject-+:|OtherUntaintedObject", FormatTainted(String.Join("|", sArr)));
    }

    [Fact]
    public void String_Join_Index_With_Both()
    {
        string[] sArr = new string[] { TaintedString, UntaintedString, OtherUntaintedString };
        Assert.Equal(":+-TaintedString-+:|UntaintedString", FormatTainted(String.Join("|", sArr, 0, 2)));
    }

    [Fact]
    public void String_Join_Index_With_Two_Tainted()
    {
        string[] sArr = new string[] { TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString };
        Assert.Equal(":+-TaintedString-+:|UntaintedString|:+-OtherTaintedString-+:", FormatTainted(String.Join("|", sArr, 0, 3)));
    }

    [Fact]
    public void String_Join_Index_With_Two_Untainted()
    {
        string[] sArr = new string[] { UntaintedString, TaintedString, OtherUntaintedString, OtherUntaintedString };
        Assert.Equal("UntaintedString|:+-TaintedString-+:|OtherUntaintedString", FormatTainted(String.Join("|", sArr, 0, 3)));
    }

    [Fact]
    public void String_Join_Index_With_Two_Tainted_And_Two_Untainted()
    {
        string[] sArr = new string[] { TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString, OtherUntaintedString };
        Assert.Equal(":+-TaintedString-+:|UntaintedString|:+-OtherTaintedString-+:|OtherUntaintedString", FormatTainted(String.Join("|", sArr, 0, 4)));
    }

    [Fact]
    public void String_Join_Index_With_Two_Tainted_And_Two_Untainted_Chunk()
    {
        string[] sArr = new string[] { TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString, OtherUntaintedString };
        Assert.Equal(":+-OtherTaintedString-+:|OtherUntaintedString", FormatTainted(String.Join("|", sArr, 2, 2)));
    }

    [Fact]
    public void String_Join_List_With_Both()
    {
        List<string> list = new List<string>() { TaintedString, UntaintedString };
        Assert.Equal(":+-TaintedString-+:|UntaintedString", FormatTainted(String.Join("|", list)));
    }

    [Fact]
    public void String_Join_List_With_Two_Tainted()
    {
        List<string> list = new List<string>() { TaintedString, UntaintedString, OtherTaintedString };
        Assert.Equal(":+-TaintedString-+:|UntaintedString|:+-OtherTaintedString-+:", FormatTainted(String.Join("|", list)));
    }

    [Fact]
    public void String_Join_List_With_Two_Untainted()
    {
        List<string> list = new List<string>() { UntaintedString, TaintedString, OtherUntaintedString };
        Assert.Equal("UntaintedString|:+-TaintedString-+:|OtherUntaintedString", FormatTainted(String.Join("|", list)));
    }

    [Fact]
    public void String_Join_List_With_Two_Tainted_And_Two_Untainted()
    {
        List<string> list = new List<string>() { TaintedString, UntaintedString, OtherTaintedString, OtherUntaintedString };
        Assert.Equal(":+-TaintedString-+:|UntaintedString|:+-OtherTaintedString-+:|OtherUntaintedString", FormatTainted(String.Join("|", list)));
    }

    [Fact]
    public void String_Join_T_Generic_Struct()
    {
        string str = string.Join<StructForStringTest>("|", new List<StructForStringTest> { new StructForStringTest(UntaintedString), new StructForStringTest(TaintedString) });
        Assert.Equal("UntaintedString|:+-TaintedString-+:", FormatTainted(str));
    }

    [Fact]
    public void String_Join_T_Generic_Class()
    {
        string str = string.Join<StructForStringTest>("|", new List<StructForStringTest> { new StructForStringTest(UntaintedString), new StructForStringTest(TaintedString) });
        Assert.Equal("UntaintedString|:+-TaintedString-+:", FormatTainted(str));
    }
}
