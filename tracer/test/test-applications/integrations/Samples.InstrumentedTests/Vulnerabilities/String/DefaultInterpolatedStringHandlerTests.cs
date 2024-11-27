#if NET6_0_OR_GREATER

using System;
using Xunit;
using System.Runtime.CompilerServices;
using FluentAssertions;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;

public class DefaultInterpolatedStringHandlerTests : InstrumentationTestsBase
{
    protected string TaintedValue = "tainted";
    protected string UntaintedValue = "untainted";

    public DefaultInterpolatedStringHandlerTests()
    {
        AddTainted(TaintedValue);
    }

    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingTaintedValueAppendFormatted1_GetString_Vulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted(TaintedValue);

        var str = test.ToStringAndClear();
        str.Should().Be(TaintedValue);
        AssertTainted(str);
    }

    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingUntaintedValueAppendFormatted1_GetString_NonVulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted(UntaintedValue);

        var str = test.ToStringAndClear();
        str.Should().Be(UntaintedValue);
        AssertNotTainted(str);
    }

    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingTaintedValueAppendFormatted2_GetString_Vulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted(TaintedValue, 0, string.Empty);

        var str = test.ToStringAndClear();
        str.Should().Be(TaintedValue);
        AssertTainted(str);
    }

    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingUntaintedValueAppendFormatted2_GetString_NonVulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted(UntaintedValue, 0, string.Empty);

        var str = test.ToStringAndClear();
        str.Should().Be(UntaintedValue);
        AssertNotTainted(str);
    }

    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingTaintedValueAppendFormatted3_GetString_Vulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted((object)TaintedValue, 0, string.Empty);

        var str = test.ToStringAndClear();
        str.Should().Be(TaintedValue);
        AssertTainted(str);
    }

    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingUntaintedValueAppendFormatted3_GetString_NonVulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted((object)UntaintedValue, 0, string.Empty);

        var str = test.ToStringAndClear();
        str.Should().Be(UntaintedValue);
        AssertNotTainted(str);
    }

    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingTaintedValueAppendFormatted4_GetString_Vulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted((object)TaintedValue);

        var str = test.ToStringAndClear();
        str.Should().Be(TaintedValue);
        AssertTainted(str);
    }

    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingUntaintedValueAppendFormatted4_GetString_NonVulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted((object)UntaintedValue);

        var str = test.ToStringAndClear();
        str.Should().Be(UntaintedValue);
        AssertNotTainted(str);
    }

    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingTaintedValueAppendFormatted5_GetString_Vulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted((object)TaintedValue, 0);

        var str = test.ToStringAndClear();
        str.Should().Be(TaintedValue);
        AssertTainted(str);
    }

    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingUntaintedValueAppendFormatted5_GetString_NonVulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted((object)UntaintedValue, 0);

        var str = test.ToStringAndClear();
        str.Should().Be(UntaintedValue);
        AssertNotTainted(str);
    }

    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingTaintedValueAppendFormatted6_GetString_Vulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted((object)TaintedValue, string.Empty);

        var str = test.ToStringAndClear();
        str.Should().Be(TaintedValue);
        AssertTainted(str);
    }

    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingUntaintedValueAppendFormatted6_GetString_NonVulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted((object)UntaintedValue, string.Empty);

        var str = test.ToStringAndClear();
        str.Should().Be(UntaintedValue);
        AssertNotTainted(str);
    }
    
    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingTaintedValueAppendFormatted7_GetString_Vulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted((object)TaintedValue, 0, string.Empty);

        var str = test.ToStringAndClear();
        str.Should().Be(TaintedValue);
        AssertTainted(str);
    }

    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingUntaintedValueAppendFormatted7_GetString_NonVulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted((object)UntaintedValue, 0, string.Empty);

        var str = test.ToStringAndClear();
        str.Should().Be(UntaintedValue);
        AssertNotTainted(str);
    }

    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingTaintedValueMultipleValues_GetString_Vulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendLiteral(UntaintedValue);
        test.AppendFormatted(new ReadOnlySpan<char>([' ', 'w', 'o', 'r', 'l', 'd', ' ']));
        test.AppendFormatted(TaintedValue);
        test.AppendFormatted(42);

        AssertTainted(test.ToStringAndClear());
    }

    [Fact]
    public void GivenAnImplicitInterpolatedString_WhenAddingTaintedValue_GetString_Vulnerable()
    {
        var number = 5;
        var str = $"Hello {TaintedValue} {number}";
        str.Should().Be("Hello " + TaintedValue + " " + number);
        AssertTainted(str);
    }

    [Fact]
    public void GivenAnImplicitInterpolatedString_WhenAddingUntaintedValue_GetString_NonVulnerable()
    {
        var number = 5;
        var str = $"Hello {UntaintedValue} {number}";
        str.Should().Be("Hello " + UntaintedValue + " " + number);
        AssertNotTainted(str);
    }

    [Fact]
    public void GivenAnImplicitInterpolatedString_WhenAddingTaintedValueAsObject_GetString_Vulnerable()
    {
        var number = 5;
        var str = $"Hello {(object)TaintedValue} {number}";
        str.Should().Be("Hello " + TaintedValue + " " + number);
        AssertTainted(str);
    }

    [Fact]
    public void GivenAnImplicitInterpolatedString_WhenAddingMultipleValuesWithTaintedValues_GetString_Vulnerable()
    {
        var order = new
        {
            CustomerId = "VINET",
            EmployeeId = 5,
            OrderDate = new DateTime(2021, 1, 1),
            RequiredDate = new DateTime(2021, 1, 1),
            ShipVia = 3,
            Freight = 32,
            ShipName = "Vins et alcools Chevalier",
            ShipAddress = TaintedValue,
            ShipCity = "Reims",
            ShipPostalCode = "51100",
            ShipCountry = "France"
        };
        
        var sql = "INSERT INTO Orders (" +
                  "CustomerId, EmployeeId, OrderDate, RequiredDate, ShipVia, Freight, ShipName, ShipAddress, " +
                  "ShipCity, ShipPostalCode, ShipCountry" +
                  ") VALUES (" +
                  $"'{order.CustomerId}','{order.EmployeeId}','{order.OrderDate:yyyy-MM-dd}','{order.RequiredDate:yyyy-MM-dd}'," +
                  $"'{order.ShipVia}','{order.Freight}','{order.ShipName}','{order.ShipAddress}'," +
                  $"'{order.ShipCity}','{order.ShipPostalCode}','{order.ShipCountry}')";
        
        sql.Should().Be("INSERT INTO Orders (CustomerId, EmployeeId, OrderDate, RequiredDate, ShipVia, Freight, ShipName, ShipAddress, ShipCity, ShipPostalCode, ShipCountry) VALUES ('VINET','5','2021-01-01','2021-01-01','3','32','Vins et alcools Chevalier','tainted','Reims','51100','France')");
        AssertTainted(sql);
    }

    [Fact]
    public void GivenImplicitInterpolatedString_WhenAddingTaintedValuesNested_GetString_Vulnerable()
    {
        const int number = 42;
        var date = new DateTime(2024, 11, 22, 15, 30, 0);
        const decimal decimalValue = 123.456m;
        const bool booleanValue = true;
        const char charValue = 'A';
        var nestedInterpolatedString1 = $"Nested1 {TaintedValue} and {number}";
        var nestedInterpolatedString2 = $"Nested2 {nestedInterpolatedString1} with date {date:yyyy-MM-dd}";
        var complexString = $"Complex {nestedInterpolatedString2} and decimal {decimalValue:F2} and boolean {booleanValue}";

        var nestedString = $"Hello {$"{TaintedValue + "Hello"} - {complexString}"}";
        var finalString = $"Final {nestedString} and char {charValue} with additional {UntaintedValue} and number {number} and date {date:HH:mm:ss}";

        finalString.Should().Be("Final Hello Hello - Complex Nested2 Nested1 tainted and 42 with date 2024-11-22 and decimal 123.46 and boolean True and char A with additional untainted and number 42 and date 15:30:00");
        AssertTainted(finalString);
    }

    [Fact]
    public void GivenImplicitInterpolatedString_WhenAddingTaintedValuesComplex_GetString_Vulnerable()
    {
        var interpolatedString = $"""
                                  Hello "{TaintedValue}" and "{UntaintedValue}".
                                  .
                                  """;
        interpolatedString.Should().Be("Hello \"tainted\" and \"untainted\".\n.");
        AssertTainted(interpolatedString);
    }
}

#endif
