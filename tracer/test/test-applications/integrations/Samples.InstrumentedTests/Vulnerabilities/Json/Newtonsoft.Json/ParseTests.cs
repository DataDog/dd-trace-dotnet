using Newtonsoft.Json.Linq;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.Json.Newtonsoft.Json;

public class ParseTests : InstrumentationTestsBase
{
    private readonly string _taintedJson = """{ "key": "value" }""";
    private readonly string _taintedJsonObjectWithArray = """{"key": ["value1", "value2", {"key2": "value"}]}""";
    private readonly string _taintedJsonArray = """["value1", "value2"]""";
    private readonly string _taintedJsonDifferentTypes = """{ "name": "Chris", "age": 23, "address": { "city": "New York", "country": "America" }, "friends": [ { "name": "Emily", "hobbies": [ "biking", "music", "gaming" ] }, { "name": "John", "hobbies": [ "soccer", "gaming" ] }, [ "aString", { "obj": "val" } ] ] }""";

    public ParseTests()
    {
        // Add all tainted values
        AddTainted(_taintedJson);
        AddTainted(_taintedJsonObjectWithArray);
        AddTainted(_taintedJsonArray);
        AddTainted(_taintedJsonDifferentTypes);
    }

    [Fact]
    public void GivenASimpleJSON_WhenParsing_Value_Vulnerable()
    {
        var json = JObject.Parse(_taintedJson);
        var keyStr = json.Value<string>("key");
        AssertTainted(keyStr);
    }
    
    [Fact]
    public void GivenASimpleJSON_WhenParsing_TryGetValue_Vulnerable()
    {
        var json = JObject.Parse(_taintedJson);
        var keyStr = json.TryGetValue("key", out var value) ? value.ToString() : null;
        AssertTainted(keyStr);
    }
    
    [Fact]
    public void GivenASimpleJSON_WhenParsing_ArrayAccess_Vulnerable()
    {
        var json = JObject.Parse(_taintedJson);
        var keyStr = json["key"]?.ToString();
        AssertTainted(keyStr);
    }

    [Fact]
    public void GivenObjectArrayJSON_WhenParsing_Vulnerable()
    {
        var json = JObject.Parse(_taintedJsonObjectWithArray);
        var val1 = json["key"]?[0]?.ToString();
        var val2 = json["key"]?[1]?.ToString();

        Assert.Equal("value1", val1);
        AssertTainted(val1);
        Assert.Equal("value2", val2);
        AssertTainted(val2);
    }
    
    [Fact]
    public void GivenAllTypesJSON_WhenParsing_Vulnerable()
    {
        var json = JObject.Parse(_taintedJsonDifferentTypes);
        var name = json["name"]?.ToString();
        var city = json["address"]?["city"]?.ToString();
        var country = json["address"]?["country"]?.ToString();
        var friend1Name = json["friends"]?[0]?["name"]?.ToString();
        var friend1Hobbies = json["friends"]?[0]?["hobbies"]?[0]?.ToString();
        var friend2Name = json["friends"]?[1]?["name"]?.ToString();
        var friend2Hobbies = json["friends"]?[1]?["hobbies"]?[0]?.ToString();
        var friend2Hobbies2 = json["friends"]?[1]?["hobbies"]?[1]?.ToString();
        var lastFriendArrayString = json["friends"]?[2]?[0]?.ToString();
        var lastFriendArrayObject = json["friends"]?[2]?[1]?["obj"]?.ToString();
        
        Assert.Equal("Chris", name);
        AssertTainted(name);
        Assert.Equal("New York", city);
        AssertTainted(city);
        Assert.Equal("America", country);
        AssertTainted(country);
        Assert.Equal("Emily", friend1Name);
        AssertTainted(friend1Name);
        Assert.Equal("biking", friend1Hobbies);
        AssertTainted(friend1Hobbies);
        Assert.Equal("John", friend2Name);
        AssertTainted(friend2Name);
        Assert.Equal("soccer", friend2Hobbies);
        AssertTainted(friend2Hobbies);
        Assert.Equal("gaming", friend2Hobbies2);
        AssertTainted(friend2Hobbies2);
        Assert.Equal("aString", lastFriendArrayString);
        AssertTainted(lastFriendArrayString);
        Assert.Equal("val", lastFriendArrayObject);
    }
    
    [Fact]
    public void GivenASimpleJSON_WhenJArrayParsing_Vulnerable()
    {
        var json = JArray.Parse(_taintedJsonArray);
        var val1 = json[0]?.ToString();
        var val2 = json[1]?.ToString();
        Assert.Equal("value1", val1);
        AssertTainted(val1);
        Assert.Equal("value2", val2);
        AssertTainted(val2);
    }
    
    [Fact]
    public void GivenASimpleJSON_WhenJTokenParsing_Vulnerable()
    {
        var json = JToken.Parse(_taintedJson);
        var keyStr = json["key"]?.ToString();
        Assert.Equal("value", keyStr);
        AssertTainted(keyStr);
    }
}
