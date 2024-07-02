using System;
using System.Text.Json;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.Json;

#if NETCOREAPP3_1_OR_GREATER

public class JsonDocumentTests : InstrumentationTestsBase
{
    private readonly string _taintedJson = """{ "key": "value" }""";
    private readonly string _taintedJsonMultiple = """{ "key1": "value1", "key2": "value2" }""";
    private readonly string _taintedJsonArray = """["value1", "value2"]""";
    private readonly string _taintedJsonDeepObject = """{ "key": { "key2": "value"}}""";
    private readonly string _taintedJsonDifferentTypes = "{ \"name\": \"Chris\", \"age\": 23, \"address\": { \"city\": \"New York\", \"country\": \"America\" }, \"friends\": [ { \"name\": \"Emily\", \"hobbies\": [ \"biking\", \"music\", \"gaming\" ] }, { \"name\": \"John\", \"hobbies\": [ \"soccer\", \"gaming\" ] }, [ \"aString\", { \"obj\": \"val\" } ] ] }";

    private readonly string _notTaintedJson = """{ "key": "value" }""";

    public JsonDocumentTests()
    {
        // Add all tainted values
        AddTainted(_taintedJson);
        AddTainted(_taintedJsonMultiple);
        AddTainted(_taintedJsonArray);
        AddTainted(_taintedJsonDeepObject);
        AddTainted(_taintedJsonDifferentTypes);
    }

    [Fact]
    public void AccessByRef_AzureDataTables_JsonElementExtensions()
    {
        var json = JsonDocument.Parse(_taintedJson);
        var element = json.RootElement.GetProperty("key");
        
        // Call with reflection the method Azure.Core.JsonElementExtensions.GetObject(JsonElement& element)
        var obj = (object)element;
        var type = Type.GetType("Azure.Core.JsonElementExtensions, Azure.Data.Tables")!;
        var method = type.GetMethod("GetObject", new[] { typeof(JsonElement).MakeByRefType() });
        var result = method!.Invoke(null, new[] { obj });
        
        Assert.Equal("value", result);
        AssertTainted(result);
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

        Assert.Equal("\"value\"", str);
        AssertTainted(str);
    }

    [Fact]
    public void GivenALotOfDifferentTypesJson_WhenParsing_GetProperty_Vulnerable()
    {
        var json = JsonDocument.Parse(_taintedJsonDifferentTypes);
        var element = json.RootElement;
        var name = element.GetProperty("name").GetString();
        var address = element.GetProperty("address");
        var city = address.GetProperty("city").GetString();
        var country = address.GetProperty("country").GetString();
        var friends = element.GetProperty("friends");
        var friend1 = friends[0];
        var friend2 = friends[1];
        var friend1Name = friend1.GetProperty("name").GetString();
        var friend1Hobbies = friend1.GetProperty("hobbies");
        var friend1Hobby1 = friend1Hobbies[0].GetString();
        var friend1Hobby2 = friend1Hobbies[1].GetString();
        var friend2Name = friend2.GetProperty("name").GetString();
        var friend2Hobbies = friend2.GetProperty("hobbies");
        var friend2Hobby1 = friend2Hobbies[0].GetString();
        var friend2Hobby2 = friend2Hobbies[1].GetString();
        var friend3 = friends[2];
        var friend3String = friend3[0].GetString();
        var friend3Obj = friend3[1];
        var friend3ObjValue = friend3Obj.GetProperty("obj").GetString();

        Assert.Equal("Chris", name);
        AssertTainted(name);
        Assert.Equal("New York", city);
        AssertTainted(city);
        Assert.Equal("America", country);
        AssertTainted(country);
        Assert.Equal("Emily", friend1Name);
        AssertTainted(friend1Name);
        Assert.Equal("biking", friend1Hobby1);
        AssertTainted(friend1Hobby1);
        Assert.Equal("music", friend1Hobby2);
        AssertTainted(friend1Hobby2);
        Assert.Equal("John", friend2Name);
        AssertTainted(friend2Name);
        Assert.Equal("soccer", friend2Hobby1);
        AssertTainted(friend2Hobby1);
        Assert.Equal("gaming", friend2Hobby2);
        AssertTainted(friend2Hobby2);
        Assert.Equal("aString", friend3String);
        AssertTainted(friend3String);
        Assert.Equal("val", friend3ObjValue);
        AssertTainted(friend3ObjValue);
    }
}

#endif
