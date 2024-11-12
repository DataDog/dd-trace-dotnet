using System.Text;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringBuilderPropagation;
public class StringBuilderInsertTests : InstrumentationTestsBase
{
    private string _taintedValue = "tainted";
    private string _taintedValue2 = "TAINTED2";
    private string _untaintedString = "UntaintedString";
    private StringBuilder _taintedStringBuilder = new StringBuilder("TaintedStringBuilder");
    private string _taintedString = "TaintedString";
    private char[] _untaintedCharArray = "UntaintedCharArray".ToCharArray();
    private char[] _taintedCharArray = "TaintedString".ToCharArray();

    public StringBuilderInsertTests()
    {
        AddTainted(_taintedValue);
        AddTainted(_taintedValue2);
        AddTainted(_taintedStringBuilder);
        AddTainted(_taintedString);
        AddTainted(_taintedCharArray);
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.String)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertNullString_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:",
            new StringBuilder(_taintedValue).Insert(3, (string)null).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, (string)null).ToString());
    }

    [Fact]
    public void GivenANullString_WhenCallingStringBuilderInsertObject_ResultIsNotTainted()
    {
        int index = 2;
        AssertUntaintedWithOriginalCallCheck(
            () => ((StringBuilder)null).Insert(index, _untaintedString),
            () => ((StringBuilder)null).Insert(index, _untaintedString));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted1()
    {
        int index = 2;
        AssertTaintedFormatWithOriginalCallCheck(":+-Ta-+:UntaintedString:+-intedStringBuilder-+:",
            _taintedStringBuilder.Insert(index, _untaintedString),
            () => _taintedStringBuilder.Insert(index, _untaintedString));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted2()
    {
        int index = 2;
        AssertTaintedFormatWithOriginalCallCheck(":+-Ta-+::+-TaintedString-+::+-intedStringBuilder-+:",
            _taintedStringBuilder.Insert(index, _taintedString),
            () => _taintedStringBuilder.Insert(index, _taintedString));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted3()
    {
        int index = 2;
        AssertTaintedFormatWithOriginalCallCheck(":+-Ta-+:UntaintedString:+-int-+::+-TaintedString-+::+-edStringBuilder-+:",
            _taintedStringBuilder.Insert(index, _untaintedString).Insert(20, _taintedString),
            () => _taintedStringBuilder.Insert(index, _untaintedString).Insert(20, _taintedString));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted4()
    {
        int index = 2;
        AssertTaintedFormatWithOriginalCallCheck(":+-Ta-+:UntaintedCharArray:+-intedStringBuilder-+:",
            _taintedStringBuilder.Insert(index, _untaintedCharArray),
            () => _taintedStringBuilder.Insert(index, _untaintedCharArray));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted5()
    {
        int index = 2;
        AssertTaintedFormatWithOriginalCallCheck(":+-Ta-+::+-TaintedString-+::+-intedStringBuilder-+:",
            _taintedStringBuilder.Insert(index, _taintedCharArray),
            () => _taintedStringBuilder.Insert(index, _taintedCharArray));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted6()
    {
        int index = 2;
        AssertTaintedFormatWithOriginalCallCheck(":+-Ta-+::+-TaintedString-+::+-inted-+:UntaintedCharArray:+-StringBuilder-+:",
            _taintedStringBuilder.Insert(index, _taintedCharArray).Insert(20, _untaintedCharArray),
            () => _taintedStringBuilder.Insert(index, _taintedCharArray).Insert(20, _untaintedCharArray));
    }


    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertString_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+:www:+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, "www").ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, "www").ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertStringTainted_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+::+-TAINTED2-+::+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, _taintedValue2).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, _taintedValue2).ToString());
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.String,System.Int32)

    [Theory]
    [InlineData(":+-tainted-+:", 3, "www", 0)]
    [InlineData(":+-tai-+:wwwwww:+-nted-+:", 3, "www", 2)]
    [InlineData(":+-tainted-+:", 3, (string)null, 2)]
    public void GivenATaintedString_WhenCallingStringBuilderInsertStringAndCount_ResultIsTainted(
        string expected, int index, string value, int count)
    {
        AssertTaintedFormatWithOriginalCallCheck(expected,
            new StringBuilder(_taintedValue).Insert(index, value, count).ToString(),
            () => new StringBuilder(_taintedValue).Insert(index, value, count).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertStringTaintedAndCount1_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+::+-TAINTED2-+::+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, _taintedValue2, 1).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, _taintedValue2, 1).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertStringTaintedAndCount_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+::+-TAINTED2-+::+-TAINTED2-+::+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, _taintedValue2, 2).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, _taintedValue2, 2).ToString());
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.Char)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertChar_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+:d:+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, 'd').ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, 'd').ToString());
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.Char[])

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertCharArray_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+:er:+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, new char[] { 'e', 'r' }).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, new char[] { 'e', 'r' }).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertCharArrayEmpty_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:",
            new StringBuilder(_taintedValue).Insert(3, new char[] { }).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, new char[] { }).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertCharArrayNull_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tainted-+:",
            new StringBuilder(_taintedValue).Insert(3, (char[])null).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, (char[])null).ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertCharArrayTainted_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+::+-TAINTED2-+::+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, _taintedValue2.ToCharArray()).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, _taintedValue2.ToCharArray()).ToString());
    }

    [Fact]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GivenATaintedString_WhenCallingStringBuilderInsertCharArrayBadIndex_ArgumentOutOfRangeException()
    {
        AssertUntaintedWithOriginalCallCheck(() => new StringBuilder(_taintedValue).Insert(-3, new char[] { 'e', 'r' }),
            () => new StringBuilder(_taintedValue).Insert(-3, new char[] { 'e', 'r' }));
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.Char[],System.Int32,System.Int32)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertCharArrayTaintedAndCount_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+::+-NTE-+::+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, _taintedValue2.ToCharArray(), 3, 3).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, _taintedValue2.ToCharArray(), 3, 3).ToString());
    }

    [Theory]
    [InlineData(-3, 3)]
    [InlineData(3, 300)]
    [InlineData(300, 3)]
    [InlineData(3, -3)]
    public void GivenATaintedString_WhenCallingStringBuilderInsertCharArrayTaintedAndWrongIndex_ArgumentOutOfRangeException(int startIndex, int charCount)
    {
        AssertUntaintedWithOriginalCallCheck(
            () => new StringBuilder(_taintedValue).Insert(3, _taintedValue2.ToCharArray(), startIndex, charCount),
            () => new StringBuilder(_taintedValue).Insert(3, _taintedValue2.ToCharArray(), startIndex, charCount));
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.Int32)

    [Fact]
    public void StringBuilder_Insert_Int_With_Untainted()
    {
        var check = new StringBuilder("TaintedStringBuilder");
        var tainted = new StringBuilder("TaintedStringBuilder");
        AddTainted(tainted);

        AssertTaintedFormatWithOriginalCallCheck("10:+-TaintedStringBuilder-+:",
            tainted.Insert(0, 10),
            () => check.Insert(0, 10));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertInt_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+:12:+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, 12).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, (int)12).ToString());
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.Int64)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted8()
    {
        AssertTaintedFormatWithOriginalCallCheck("9223372036854775807:+-TaintedStringBuilder-+:",
            _taintedStringBuilder.Insert(0, 9223372036854775807),
            () => _taintedStringBuilder.Insert(0, 9223372036854775807));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertLong_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+:12:+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, (long)12).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, (long)12).ToString());
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.Single)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted9()
    {
        float value = 1.001f;
        AssertTaintedFormatWithOriginalCallCheck(value.ToString() + ":+-TaintedStringBuilder-+:",
            _taintedStringBuilder.Insert(0, value),
            () => _taintedStringBuilder.Insert(0, value));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertfloat_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+:33:+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, (float)33).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, (float)33).ToString());
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.Double)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted10()
    {
        double number = 12.3;
        AssertTaintedFormatWithOriginalCallCheck(number.ToString() + ":+-TaintedStringBuilder-+:",
            _taintedStringBuilder.Insert(0, number),
            () => _taintedStringBuilder.Insert(0, number));
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.Decimal)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted11()
    {
        decimal number = 2.1m;
        AssertTaintedFormatWithOriginalCallCheck(number.ToString() + ":+-TaintedStringBuilder-+:",
            _taintedStringBuilder.Insert(0, number),
            () => _taintedStringBuilder.Insert(0, number));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertdecimal_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+:33:+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, (decimal)33).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, (decimal)33).ToString());
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.UInt16)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted12()
    {
        ushort number = 2;
        AssertTaintedFormatWithOriginalCallCheck("2:+-TaintedStringBuilder-+:",
            _taintedStringBuilder.Insert(0, number),
            () => _taintedStringBuilder.Insert(0, number));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertushort_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+:33:+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, (ushort)33).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, (ushort)33).ToString());
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.UInt32)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted14()
    {
        uint number = 4294967295;
        AssertTaintedFormatWithOriginalCallCheck("4294967295:+-TaintedStringBuilder-+:",
            _taintedStringBuilder.Insert(0, number),
            () => _taintedStringBuilder.Insert(0, number));
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertuint_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+:33:+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, (uint)33).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, (uint)33).ToString());
    }
    // test System.Text.StringBuilder::Insert(System.Int32,System.UInt64)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted13()
    {
        ulong number = 18446744073709551615;
        AssertTaintedFormatWithOriginalCallCheck("18446744073709551615:+-TaintedStringBuilder-+:",
            _taintedStringBuilder.Insert(0, number),
            () => _taintedStringBuilder.Insert(0, number));
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.Boolean)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted7()
    {
        AssertTaintedFormatWithOriginalCallCheck("True:+-TaintedStringBuilder-+:",
            _taintedStringBuilder.Insert(0, true),
            () => _taintedStringBuilder.Insert(0, true));
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.SByte)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertsbyte_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+:33:+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, (sbyte)33).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, (sbyte)33).ToString());
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.Byte)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertByte_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+:12:+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, (byte)12).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, (byte)12).ToString());
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.Int16)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertshort_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+:33:+-nted-+:",
            new StringBuilder(_taintedValue).Insert(3, (short)33).ToString(),
            () => new StringBuilder(_taintedValue).Insert(3, (short)33).ToString());
    }

    // test System.Text.StringBuilder::Insert(System.Int32,System.Object)

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObject_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+:www:+-nted-+:", 
            new StringBuilder(_taintedValue).Insert(3, (object)"www").ToString(), 
            () => new StringBuilder(_taintedValue).Insert(3, (object)"www").ToString());
    }

    [Fact]
    public void GivenATaintedString_WhenCallingStringBuilderInsertObjectTainted_ResultIsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(":+-tai-+::+-TAINTED2-+::+-nted-+:", 
            new StringBuilder(_taintedValue).Insert(3, (object)_taintedValue2).ToString(), 
            () => new StringBuilder(_taintedValue).Insert(3, (object)_taintedValue2).ToString());
    }
}
