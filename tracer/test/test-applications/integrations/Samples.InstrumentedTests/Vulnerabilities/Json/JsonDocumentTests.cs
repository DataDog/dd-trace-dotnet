using System;
using System.Text.Json;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.Json;

#if NETCOREAPP3_0_OR_GREATER

public class JsonDocumentTests : InstrumentationTestsBase
{
    private readonly string _taintedJson = "{ \"key\": \"value\" }";
    private readonly string _taintedJsonMultiple = "{\"key1\": \"value1\", \"key2\": \"value2\"}";
    private readonly string _taintedJsonArray = "[\"value1\", \"value2\"]";
    private readonly string _taintedJsonDeepObject = "{\"key\": {\"key2\": \"value\"}}";

    private readonly string _notTaintedJson = "{ \"key\": \"value\" }";

    public JsonDocumentTests()
    {
        // Add all tainted values
        AddTainted(_taintedJson);
        AddTainted(_taintedJsonMultiple);
        AddTainted(_taintedJsonArray);
        AddTainted(_taintedJsonDeepObject);
    }

    [Fact]
    public void GivenASimpleJson_WhenParsing_GetProperty_Vulnerable()
    {
        var json = JsonDocument.Parse(_taintedJson);
        var element = json.RootElement;
        var value = element.GetProperty("key");
        var str = value.GetString();

        Assert.Equal("value", str);
        AssertTainted(str);
    }
    
    [Fact]
    public void GivenASimpleJson_WhenParsing_GetProperty_NotVulnerable()
    {
        var json = JsonDocument.Parse(_notTaintedJson);
        var element = json.RootElement;
        var value = element.GetProperty("key");
        var str = value.ToString();

        Assert.Equal("value", str);
        AssertNotTainted(str);
    }
    
    [Fact]
    public void GivenAJsonArray_WhenParsing_GetProperty_Vulnerable()
    {
        var json = JsonDocument.Parse(_taintedJsonArray);
        var element = json.RootElement;
        var value1 = element[0];
        var value2 = element[1];
        var str1 = value1.GetString();
        var str2 = value2.GetString();
        
        Assert.Equal("value1", str1);
        AssertTainted(str1);
        Assert.Equal("value2", str2);
        AssertTainted(str2);
    }
    
    [Fact]
    public void GivenAMultipleJson_WhenParsing_GetProperty_Vulnerable()
    {
        var json = JsonDocument.Parse(_taintedJsonMultiple);
        var element = json.RootElement;
        var value = element.GetProperty("key2");
        var str = value.GetString();

        Assert.Equal("value2", str);
        AssertTainted(str);
    }
    
    [Fact]
    public void GivenASimpleJson_WhenParsing_GetProperty_GetRawText_Vulnerable()
    {
        var json = JsonDocument.Parse(_taintedJson);
        var element = json.RootElement;
        var value = element.GetProperty("key");
        var str = value.GetRawText();

        Assert.Equal("value", str);
        AssertTainted(str);
    }
}

#endif
