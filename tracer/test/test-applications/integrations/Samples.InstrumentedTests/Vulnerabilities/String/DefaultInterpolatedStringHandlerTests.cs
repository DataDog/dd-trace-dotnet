#if NET6_0_OR_GREATER

using System;
using Xunit;
using System.Runtime.CompilerServices;

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

        AssertTainted(test.ToStringAndClear());
    }
    
    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingUntaintedValueAppendFormatted1_GetString_NonVulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted(UntaintedValue);

        AssertNotTainted(test.ToStringAndClear());
    }
    
    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingTaintedValueAppendFormatted2_GetString_Vulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted(TaintedValue, 0);

        AssertTainted(test.ToStringAndClear());
    }
    
    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingUntaintedValueAppendFormatted2_GetString_NonVulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted(UntaintedValue, 0);

        AssertNotTainted(test.ToStringAndClear());
    }
    
    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingTaintedValueAppendFormatted3_GetString_Vulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted(TaintedValue, 0, null);

        AssertTainted(test.ToStringAndClear());
    }
    
    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingUntaintedValueAppendFormatted3_GetString_NonVulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted(UntaintedValue, 0, null);

        AssertNotTainted(test.ToStringAndClear());
    }
    
    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingTaintedValueAppendFormattedTObject_GetString_Vulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted((object)TaintedValue);

        AssertTainted(test.ToStringAndClear());
    }
    
    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingTaintedValueAppendLiteral_GetString_Vulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendLiteral(TaintedValue);

        AssertTainted(test.ToStringAndClear());
    }
    
    [Fact]
    public void GivenAnExplicitInterpolatedString_WhenAddingUntaintedValueAppendLiteral_GetString_NonVulnerable()
    {
        var test = new DefaultInterpolatedStringHandler();
        test.AppendFormatted(UntaintedValue);

        AssertNotTainted(test.ToStringAndClear());
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
        AssertTainted(str);
    }

    [Fact]
    public void GivenAnImplicitInterpolatedString_WhenAddingUntaintedValue_GetString_NonVulnerable()
    {
        var number = 5;
        var str = $"Hello {UntaintedValue} {number}";
        AssertNotTainted(str);
    }
    
    [Fact]
    public void GivenAnImplicitInterpolatedString_WhenAddingTaintedValueAsObject_GetString_Vulnerable()
    {
        var number = 5;
        var str = $"Hello {(object)TaintedValue} {number}";
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
            Freight = 32.38M,
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

        AssertTainted(finalString);
    }

    [Fact]
    public void GivenImplicitInterpolatedString_WhenAddingTaintedValuesComplex_GetString_Vulnerable()
    {
        var interpolatedString = $"""
                                  Hello "{TaintedValue}" and "{UntaintedValue}".
                                  .
                                  """;
        AssertTainted(interpolatedString);
    }
}

#endif
