using System;
using System.Text;
using Xunit;
namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringBuilderPropagation;

public class StringBuilderAppendTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string taintedValue2 = "TAINTED2";
    StringBuilder TaintedStringBuilder = new StringBuilder("TaintedStringBuilder");
    StringBuilder TaintedStringBuilder2 = new StringBuilder("TaintedStringBuilder");
    string TaintedString = "TaintedString";
    string UntaintedString = "UntaintedString";
    char[] TaintedCharArray = "TaintedString".ToCharArray();
    char[] UntaintedCharArray = "UntaintedCharArray".ToCharArray();

    public StringBuilderAppendTests()
    {
        AddTainted(taintedValue);
        AddTainted(taintedValue2);
        AddTainted(TaintedString);
        AddTainted(TaintedStringBuilder);
        AddTainted(TaintedStringBuilder2);
        AddTainted(TaintedCharArray);
    }

    // System.Text.StringBuilder::Append(System.String)

    [Fact]
    public void GivenAStringBuilder_WhenAppendBasic_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:", new StringBuilder().Append(TaintedString), () => new StringBuilder().Append(TaintedString));
    }

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendBasicWithUnTainted_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+:UntaintedString", TaintedStringBuilder.Append(UntaintedString), () => TaintedStringBuilder2.Append(UntaintedString));
    }

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendBasicWithTainted_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+::+-TaintedString-+:", TaintedStringBuilder.Append(TaintedString), () => TaintedStringBuilder2.Append(TaintedString));
    }

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendBasicWithBoth_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+:UntaintedString:+-TaintedString-+:",
            TaintedStringBuilder.Append(UntaintedString).Append(TaintedString),
            () => TaintedStringBuilder2.Append(UntaintedString).Append(TaintedString));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringEmptyBuilderAppendAppendAppend_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("pre:+-tainted-+:post", new StringBuilder().Append("pre").Append(taintedValue).Append("post").ToString(), () => new StringBuilder().Append("pre").Append(taintedValue).Append("post").ToString());
    }

    [Fact]
    public void Given2TaintedStrings_WhenCallingStringBuilderManyAppends_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("pre:+-tainted-+:middle:+-TAINTED2-+:post", new StringBuilder().Append("pre").Append(taintedValue).Append("middle").Append(taintedValue2).Append("post").ToString(), () => new StringBuilder().Append("pre").Append(taintedValue).Append("middle").Append(taintedValue2).Append("post").ToString());
    }

    [Fact]
    public void GivenATaintedStringBuilder_WhenAppendNull_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:", new StringBuilder(taintedValue).Append((string)null).ToString(), () => new StringBuilder(taintedValue).Append((string)null).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppend_ResultIsTainted14()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:",
            new StringBuilder(taintedValue).Append((string)null).ToString(),
            () => new StringBuilder(taintedValue).Append((string)null).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringEmptyBuilderAppendAppendAppendToString_ResultIsTainted2()
    {
        AssertTainted(new StringBuilder().Append("pre").Append(taintedValue).Append("post").ToString().ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringEmptyBuilderAppendAppendAppendToString_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("pre:+-tainted-+:post", new StringBuilder().Append("pre").Append(taintedValue).Append("post").ToString().ToString(), () => new StringBuilder().Append("pre").Append(taintedValue).Append("post").ToString().ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingParamStringBuilder_ResultIsTainted()
    {
        auxMethod("1", "2", "3", "4", "5", "6");
    }

    private void auxMethod(string a1, string a2, string a3, string a4, string a5, string a6)
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:123456",
            new StringBuilder().Append(taintedValue + a1 + a2 + a3 + a4 + a5 + a6),
            () => new StringBuilder().Append(taintedValue + a1 + a2 + a3 + a4 + a5 + a6));
    }

    // System.Text.StringBuilder::Append(System.Text.StringBuilder)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringBuilder_ResultIsTainted()
    {
        var sb2 = new StringBuilder(taintedValue);
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-tainted-+:",
            new StringBuilder(taintedValue).Append(sb2).ToString(),
            () => new StringBuilder(taintedValue).Append(sb2).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringBuilder_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:",
            new StringBuilder(taintedValue).Append((StringBuilder)null).ToString(),
            () => new StringBuilder(taintedValue).Append((StringBuilder)null).ToString());
    }

    // System.Text.StringBuilder::Append(System.String,System.Int32,System.Int32)

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendIndex_ThenResultIsTainted()
    {
        int start = 0;
        int count = 5;

        AssertTaintedFormatWithOriginalCallCheck(":+-Taint-+:", new StringBuilder().Append(TaintedString, start, count), () => new StringBuilder().Append(TaintedString, start, count));
    }

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendIndexWithUnTainted_ThenResultIsTainted()
    {
        int start = 0;
        int count = 7;
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+:Untaint",
            TaintedStringBuilder.Append(UntaintedString, start, count),
            () => TaintedStringBuilder2.Append(UntaintedString, start, count));
    }

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendIndexWithTainted_ThenResultIsTainted()
    {
        int start = 0;
        int count = 7;
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+::+-Tainted-+:",
            TaintedStringBuilder.Append(TaintedString, start, count),
            () => TaintedStringBuilder2.Append(TaintedString, start, count));
    }

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendIndexWithBoth()
    {
        int start = 0;
        int count = 15;
        int count2 = 13;

        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+:UntaintedString:+-TaintedString-+:",
            TaintedStringBuilder.Append(UntaintedString, start, count).Append(TaintedString, start, count2),
            () => TaintedStringBuilder2.Append(UntaintedString, start, count).Append(TaintedString, start, count2));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringIndexes_ResultIsTainted7()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-tainted-+:",
            new StringBuilder(taintedValue).Append(taintedValue, 0, taintedValue.Length).ToString(),
            () => new StringBuilder(taintedValue).Append(taintedValue, 0, taintedValue.Length).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringIndexes_ResultIsTainted8()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-ain-+:",
            new StringBuilder(taintedValue).Append(taintedValue, 1, 3).ToString(),
            () => new StringBuilder(taintedValue).Append(taintedValue, 1, 3).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringEmptyBuilderAppendAppendAppendWithParams_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("re:+-aint-+:po", new StringBuilder().Append("pre", 1, 2).Append(taintedValue, 1, 4).Append("post", 0, 2).ToString(), () => new StringBuilder().Append("pre", 1, 2).Append(taintedValue, 1, 4).Append("post", 0, 2).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringBuilder_ResultIsTainted9()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).Append(taintedValue, 1, 377).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringIndexes_ResultIsTainted10()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).Append(taintedValue, 1, -377).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringIndexes_ResultIsTainted11()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).Append(taintedValue, 100, 3).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringIndexes_ResultIsTainted12()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).Append(taintedValue, -100, 3).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringIndexes_ResultIsTainted13()
    {
        Assert.Throws<ArgumentNullException>(() => new StringBuilder(taintedValue).Append((string)null, 1, 3).ToString());
    }

    // System.Text.StringBuilder::Append(System.Text.StringBuilder,System.Int32,System.Int32)

#if NETCOREAPP3_1_OR_GREATER

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringBuilderIndexes_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-tainted-+:",
            new StringBuilder(taintedValue).Append(new StringBuilder(taintedValue), 0, taintedValue.Length).ToString(),
            () => new StringBuilder(taintedValue).Append(new StringBuilder(taintedValue), 0, taintedValue.Length).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringBuilderIndexes_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-ain-+:",
            new StringBuilder(taintedValue).Append(new StringBuilder(taintedValue), 1, 3).ToString(),
            () => new StringBuilder(taintedValue).Append(new StringBuilder(taintedValue), 1, 3).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringBuilderIndexes_ResultIsTainted3()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).Append(new StringBuilder(taintedValue), 1, 377).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringBuilderIndexes_ResultIsTainted4()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).Append(new StringBuilder(taintedValue), 1, -377).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringBuilderIndexes_ResultIsTainted5()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).Append(new StringBuilder(taintedValue), 100, 3).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringBuilderIndexes_ResultIsTainted6()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).Append(new StringBuilder(taintedValue), -100, 3).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringBuilderIndexes_ResultIsTainted16()
    {
        Assert.Throws<ArgumentNullException>(() => new StringBuilder(taintedValue).Append((StringBuilder)null, 1, 3).ToString());
    }
#endif

    // System.Text.StringBuilder::Append(System.Char[],System.Int32,System.Int32)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendCharArrayIndexes_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-tainted-+:",
            new StringBuilder(taintedValue).Append(taintedValue.ToCharArray(), 0, taintedValue.Length).ToString(),
            () => new StringBuilder(taintedValue).Append(taintedValue.ToCharArray(), 0, taintedValue.Length).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendCharArrayIndexes_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-ain-+:",
            new StringBuilder(taintedValue).Append(taintedValue.ToCharArray(), 1, 3).ToString(),
            () => new StringBuilder(taintedValue).Append(taintedValue.ToCharArray(), 1, 3).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendCharArrayIndexes_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).Append(taintedValue.ToCharArray(), 1, 377).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendCharArrayIndexes_ExceptionIsThrown2()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).Append(taintedValue.ToCharArray(), 1, -377).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendCharArrayIndexes_ExceptionIsThrown3()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).Append(taintedValue.ToCharArray(), 100, 3).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendCharArrayIndexes_ExceptionIsThrown4()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).Append(taintedValue.ToCharArray(), -100, 3).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendCharArrayIndexes_ExceptionIsThrown5()
    {
        Assert.Throws<ArgumentNullException>(() => new StringBuilder(taintedValue).Append((char[])null, 1, 3).ToString());
    }

    // System.Text.StringBuilder::Append(System.Object)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendObject_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-tainted-+:",
            new StringBuilder(taintedValue).Append(new StructForStringTest(taintedValue)).ToString(),
            () => new StringBuilder(taintedValue).Append(new StructForStringTest(taintedValue)).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendObject_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-tainted-+:",
            new StringBuilder(taintedValue).Append(new ClassForStringTest(taintedValue)).ToString(),
            () => new StringBuilder(taintedValue).Append(new ClassForStringTest(taintedValue)).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendObject_ResultIsTainted3()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:",
            new StringBuilder(taintedValue).Append((object)null).ToString(),
            () => new StringBuilder(taintedValue).Append((object)null).ToString());
    }

    [Fact]
    public void GivenAStringBuilder_WhenAppendTaintedObject_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck("pre:+-tainted-+:", new StringBuilder("pre").Append((object)taintedValue).ToString(), () => new StringBuilder("pre").Append((object)taintedValue).ToString());
    }

    [Fact]
    public void GivenATaintedStringBuilder_WhenAppendObject_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:post", new StringBuilder(taintedValue).Append((object)"post").ToString(), () => new StringBuilder(taintedValue).Append((object)"post").ToString());
    }

    [Fact]
    public void GivenATaintedStringBuilder_WhenAppendObject_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:",
            new StringBuilder(taintedValue).Append((object)null),
            () => new StringBuilder(taintedValue).Append((object)null));
    }

    [Fact]
    public void GivenATaintedStringBuilder_WhenAppendObject_ResultIsTainted3()
    {
        var sb2 = new StringBuilder(taintedValue);
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-tainted-+:",
            new StringBuilder(taintedValue).Append((object) sb2).ToString(),
            () => new StringBuilder(taintedValue).Append((object) sb2).ToString());
    }

    // System.Text.StringBuilder::Append(System.Char[])

    [Fact]
    public void GivenATaintedStringBuilder_WhenAppendCharArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-tainted-+:", new StringBuilder(taintedValue).Append(taintedValue.ToCharArray()).ToString(), () => new StringBuilder(taintedValue).Append(taintedValue.ToCharArray()).ToString());
    }

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendChars_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:", new StringBuilder().Append(TaintedCharArray), () => new StringBuilder().Append(TaintedCharArray));
    }

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendCharsWithUnTainted_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+:UntaintedCharArray",
            TaintedStringBuilder.Append(UntaintedCharArray),
            () => TaintedStringBuilder2.Append(UntaintedCharArray));
    }

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendCharsWithTainted_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+::+-TaintedString-+:",
            TaintedStringBuilder.Append(TaintedCharArray),
            () => TaintedStringBuilder2.Append(TaintedCharArray));
    }

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendCharsWithBoth_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+:UntaintedCharArray:+-TaintedString-+:",
            TaintedStringBuilder.Append(UntaintedCharArray).Append(TaintedCharArray),
            () => TaintedStringBuilder2.Append(UntaintedCharArray).Append(TaintedCharArray));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendCharArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+::+-tainted-+:",
            new StringBuilder(taintedValue).Append(taintedValue.ToCharArray()).ToString(),
            () => new StringBuilder(taintedValue).Append(taintedValue.ToCharArray()).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendStringBuilderIndexes_ResultIsTainted18()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:",
            new StringBuilder(taintedValue).Append((char[])null).ToString(),
            () => new StringBuilder(taintedValue).Append((char[])null).ToString());
    }

    // System.Text.StringBuilder::AppendLine(System.String)

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendLineBasic_ThenResultIsTainted()
    {
        StringBuilder strb = new StringBuilder();
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedString-+:" + Environment.NewLine, strb.AppendLine(TaintedString), () => strb.AppendLine(TaintedString));
    }

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendLineBasicWithUnTainted_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+:UntaintedString" + Environment.NewLine,
            TaintedStringBuilder.AppendLine(UntaintedString),
            () => TaintedStringBuilder.AppendLine(UntaintedString));
    }

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendLineBasicWithTainted_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+::+-TaintedString-+:" + Environment.NewLine,
            TaintedStringBuilder.AppendLine(TaintedString),
            () => TaintedStringBuilder.AppendLine(TaintedString));
    }

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendLineBasicWithBoth_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+:UntaintedString" + Environment.NewLine + ":+-TaintedString-+:" + Environment.NewLine,
            TaintedStringBuilder.AppendLine(UntaintedString).AppendLine(TaintedString),
            () => TaintedStringBuilder.AppendLine(UntaintedString).AppendLine(TaintedString));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendLine_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:" + Environment.NewLine, new StringBuilder(taintedValue).AppendLine().ToString(), () => new StringBuilder(taintedValue).AppendLine().ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendLineString_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:rrr" + Environment.NewLine, new StringBuilder(taintedValue).AppendLine("rrr").ToString(), () => new StringBuilder(taintedValue).AppendLine("rrr").ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderAppendLineNull_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:" + Environment.NewLine, new StringBuilder(taintedValue).AppendLine(null).ToString(), () => new StringBuilder(taintedValue).AppendLine(null).ToString());
    }

    // Not covered by aspects

    [Fact]
    public void GivenATaintedStringBuilder_WhenAppendChar_ResultIsTainted2()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:wwww",
            new StringBuilder(taintedValue).Append('w', 4).ToString(),
            () => new StringBuilder(taintedValue).Append('w', 4).ToString());
    }

    [Fact]
    public void GivenATaintedStringBuilder_WhenAppendChar_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:w",
            new StringBuilder(taintedValue).Append('w').ToString(),
            () => new StringBuilder(taintedValue).Append('w').ToString());
    }

    [Fact]
    public void GivenATaintedStringBuilder_WhenAppendChar_ResultIsTainted3()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringBuilder(taintedValue).Append('w', -4).ToString());
    }

    [Fact]
    public void GivenAStringBuilder_WhenStringBuilderAppendLineNoParams_ThenResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-TaintedStringBuilder-+:" + Environment.NewLine, TaintedStringBuilder.AppendLine(), () => TaintedStringBuilder.AppendLine());
    }
}

